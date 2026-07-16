using System.Diagnostics;
using System.Diagnostics.Metrics;
using PipelineGuard.Contracts;

namespace PipelineGuard.Api;

internal static class PipelineGuardTelemetry
{
    public const string ServiceName = "PipelineGuard.Api";
    public const string SourceName = "PipelineGuard.Api";
    public const string MeterName = "PipelineGuard.Api";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> IngestedRuns =
        Meter.CreateCounter<long>("pipelineguard.runs.ingested", unit: "{run}");

    private static readonly Counter<long> Findings =
        Meter.CreateCounter<long>("pipelineguard.findings.ingested", unit: "{finding}");

    private static readonly Histogram<double> IngestDuration =
        Meter.CreateHistogram<double>("pipelineguard.report.ingest.duration", unit: "ms");

    public static void RecordAcceptedRun(ValidationReport report, double elapsedMilliseconds)
    {
        IngestedRuns.Add(1, new KeyValuePair<string, object?>("outcome", "accepted"));
        RecordFindings("error", report.Summary.Errors);
        RecordFindings("warning", report.Summary.Warnings);
        RecordFindings("info", report.Summary.Infos);
        IngestDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("outcome", "accepted"));
    }

    public static void RecordRejectedRun(string reason, double elapsedMilliseconds)
    {
        IngestedRuns.Add(1,
            new KeyValuePair<string, object?>("outcome", "rejected"),
            new KeyValuePair<string, object?>("reason", reason));
        IngestDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("outcome", "rejected"));
    }

    private static void RecordFindings(string severity, int count)
    {
        if (count > 0)
        {
            Findings.Add(count, new KeyValuePair<string, object?>("severity", severity));
        }
    }
}
