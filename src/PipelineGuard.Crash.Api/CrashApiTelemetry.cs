using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PipelineGuard.Crash.Api;

internal static class CrashApiTelemetry
{
    public const string ServiceName = "pipelineguard-crash-api";
    public const string SourceName = "PipelineGuard.Crash.Api";
    public const string MeterName = "PipelineGuard.Crash.Api";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Ingestion =
        Meter.CreateCounter<long>("pipelineguard.crash.ingestion", unit: "{report}");

    private static readonly Histogram<double> IngestionDuration =
        Meter.CreateHistogram<double>("pipelineguard.crash.ingestion.duration", unit: "ms");

    private static readonly Histogram<double> SearchDuration =
        Meter.CreateHistogram<double>("pipelineguard.crash.search.duration", unit: "ms");

    public static void RecordIngestion(string outcome, double milliseconds)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);
        Ingestion.Add(1, tag);
        IngestionDuration.Record(milliseconds, tag);
    }

    public static void RecordSearch(string outcome, double milliseconds) =>
        SearchDuration.Record(milliseconds, new KeyValuePair<string, object?>("outcome", outcome));
}
