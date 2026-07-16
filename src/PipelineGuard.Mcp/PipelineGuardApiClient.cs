using System.Net;
using PipelineGuard.Contracts;
using PipelineGuard.Core;

namespace PipelineGuard.Mcp;

public sealed class PipelineGuardApiClient(HttpClient httpClient)
{
    private const int MaxResponseBytes = 1_100_000;

    public async Task<string> ListRunsAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        string path = $"api/v1/runs?limit={limit}&offset={offset}";
        using HttpResponseMessage response = await httpClient.GetAsync(
            path,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadBoundedAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ValidationReport?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            $"api/v1/runs/{runId:D}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string json = await ReadBoundedAsync(response, cancellationToken).ConfigureAwait(false);
        return JsonReportSerializer.Deserialize(json);
    }

    private static async Task<string> ReadBoundedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw new InvalidDataException("PipelineGuard API response exceeded the allowed size.");
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaxResponseBytes)
            {
                throw new InvalidDataException("PipelineGuard API response exceeded the allowed size.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
}
