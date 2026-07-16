using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;
using PipelineGuard.Contracts;

namespace PipelineGuard.Mcp;

[McpServerToolType]
public sealed class ValidationTools(PipelineGuardApiClient apiClient)
{
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [McpServerTool(Name = "validation_list_runs")]
    [Description("Lists recent validation runs using bounded pagination. This tool is read-only.")]
    public Task<string> ListRunsAsync(
        [Description("Number of runs to return, from 1 to 100.")] int limit = 20,
        [Description("Zero-based result offset, from 0 to 100000.")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("validation_list_runs", async () =>
        {
            ValidatePage(limit, offset);
            return await apiClient.ListRunsAsync(limit, offset, cancellationToken).ConfigureAwait(false);
        });
    }

    [McpServerTool(Name = "validation_get_summary")]
    [Description("Returns bounded metadata and severity totals for one validation run. This tool is read-only.")]
    public Task<string> GetSummaryAsync(
        [Description("Validation run ID as a GUID.")] string runId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("validation_get_summary", async () =>
        {
            ValidationReport report = await GetRequiredRunAsync(runId, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                report.SchemaVersion,
                report.RunId,
                report.ToolVersion,
                report.StartedUtc,
                report.CompletedUtc,
                report.Project,
                report.EngineVersion,
                report.Profile,
                report.Scope,
                report.Summary
            }, JsonOptions);
        });
    }

    [McpServerTool(Name = "validation_list_findings")]
    [Description("Lists findings from one run with a minimum severity and bounded pagination. This tool is read-only.")]
    public Task<string> ListFindingsAsync(
        [Description("Validation run ID as a GUID.")] string runId,
        [Description("Minimum severity: info, warning, or error.")] string minimumSeverity = "info",
        [Description("Number of findings to return, from 1 to 100.")] int limit = 20,
        [Description("Zero-based finding offset, from 0 to 100000.")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("validation_list_findings", async () =>
        {
            ValidatePage(limit, offset);
            ValidationSeverity minimum = ParseSeverity(minimumSeverity);
            ValidationReport report = await GetRequiredRunAsync(runId, cancellationToken)
                .ConfigureAwait(false);
            ValidationFinding[] findings = report.Findings
                .Where(finding => (int)finding.Severity >= (int)minimum)
                .OrderByDescending(finding => finding.Severity)
                .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.AssetPath, StringComparer.Ordinal)
                .Skip(offset)
                .Take(limit)
                .ToArray();

            return JsonSerializer.Serialize(new
            {
                report.RunId,
                minimumSeverity = minimum.ToString().ToLowerInvariant(),
                offset,
                limit,
                items = findings
            }, JsonOptions);
        });
    }

    [McpServerTool(Name = "validation_get_remediation")]
    [Description("Returns the remediation text for one stable finding ID. This tool is read-only.")]
    public Task<string> GetRemediationAsync(
        [Description("Validation run ID as a GUID.")] string runId,
        [Description("Stable finding ID from validation_list_findings.")] string findingId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("validation_get_remediation", async () =>
        {
            if (string.IsNullOrWhiteSpace(findingId) || findingId.Length > 256)
            {
                throw new ArgumentException("findingId must contain 1 to 256 characters.", nameof(findingId));
            }

            ValidationReport report = await GetRequiredRunAsync(runId, cancellationToken)
                .ConfigureAwait(false);
            ValidationFinding? finding = report.Findings.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, findingId, StringComparison.Ordinal));
            if (finding is null)
            {
                throw new KeyNotFoundException("The requested finding does not exist in this run.");
            }

            return JsonSerializer.Serialize(new
            {
                report.RunId,
                finding.Id,
                finding.RuleId,
                finding.AssetPath,
                finding.Message,
                finding.Remediation
            }, JsonOptions);
        });
    }

    private async Task<ValidationReport> GetRequiredRunAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(runId, out Guid parsedRunId))
        {
            throw new ArgumentException("runId must be a GUID.", nameof(runId));
        }

        ValidationReport? report = await apiClient.GetRunAsync(parsedRunId, cancellationToken)
            .ConfigureAwait(false);
        return report ?? throw new KeyNotFoundException("The requested validation run was not found.");
    }

    private static ValidationSeverity ParseSeverity(string value)
    {
        if (!Enum.TryParse(value, ignoreCase: true, out ValidationSeverity severity) ||
            !Enum.IsDefined(severity))
        {
            throw new ArgumentException("minimumSeverity must be info, warning, or error.", nameof(value));
        }

        return severity;
    }

    private static void ValidatePage(int limit, int offset)
    {
        if (limit is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be from 1 to 100.");
        }

        if (offset is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "offset must be from 0 to 100000.");
        }
    }

    private static async Task<string> ExecuteAsync(string toolName, Func<Task<string>> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = McpTelemetry.ActivitySource.StartActivity(toolName);
        try
        {
            string result = await operation().ConfigureAwait(false);
            McpTelemetry.Record(toolName, "success", stopwatch.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            McpTelemetry.Record(toolName, "error", stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
