using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.Crash.Worker;

public sealed class ElasticsearchCrashIndex : ICrashIndex
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ElasticsearchOptions _options;

    public ElasticsearchCrashIndex(HttpClient httpClient, IOptions<ElasticsearchOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync("_cluster/health?local=true", cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage head = await _httpClient
                .SendAsync(new HttpRequestMessage(HttpMethod.Head, _options.CrashIndex), cancellationToken)
                .ConfigureAwait(false);
            if (head.IsSuccessStatusCode)
            {
                return;
            }

            const string mapping = """
            {
              "settings": {
                "number_of_shards": 1,
                "number_of_replicas": 0,
                "refresh_interval": "1s"
              },
              "mappings": {
                "dynamic": "strict",
                "properties": {
                  "crashId": { "type": "keyword" },
                  "occurredAtUtc": { "type": "date" },
                  "receivedAtUtc": { "type": "date" },
                  "processedAtUtc": { "type": "date" },
                  "project": { "type": "keyword" },
                  "build": {
                    "properties": {
                      "version": { "type": "keyword" },
                      "branch": { "type": "keyword" },
                      "changelist": { "type": "long" }
                    }
                  },
                  "platform": { "type": "keyword" },
                  "errorType": { "type": "keyword" },
                  "errorMessage": { "type": "text" },
                  "stackTrace": { "type": "text", "index": false },
                  "normalizedStackTrace": { "type": "text" },
                  "fingerprint": { "type": "keyword" },
                  "metadata": { "type": "flattened" }
                }
              }
            }
            """;
            using var request = new HttpRequestMessage(HttpMethod.Put, _options.CrashIndex)
            {
                Content = new StringContent(mapping, Encoding.UTF8, "application/json")
            };
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.BadRequest &&
                details.Contains("resource_already_exists_exception", StringComparison.Ordinal))
            {
                return;
            }

            throw CreateIndexException(response.StatusCode, "create the crash index", details);
        }
        catch (HttpRequestException exception)
        {
            throw new CrashIndexTransientException("Elasticsearch is unavailable.", exception);
        }
    }

    public async Task<CrashIndexWriteOutcome> TryCreateAsync(
        CrashDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"{_options.CrashIndex}/_doc/{document.CrashId:D}?op_type=create",
                document,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
            {
                return CrashIndexWriteOutcome.Created;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return CrashIndexWriteOutcome.Duplicate;
            }

            string details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateIndexException(response.StatusCode, "index a crash", details);
        }
        catch (HttpRequestException exception)
        {
            throw new CrashIndexTransientException("Elasticsearch is unavailable.", exception);
        }
    }

    private static Exception CreateIndexException(
        HttpStatusCode statusCode,
        string operation,
        string details)
    {
        string message = $"Elasticsearch could not {operation} (HTTP {(int)statusCode}): {details}";
        int code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500
            ? new CrashIndexTransientException(message)
            : new CrashIndexPermanentException(message);
    }
}
