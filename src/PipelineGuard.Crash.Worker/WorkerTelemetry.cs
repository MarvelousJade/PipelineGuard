using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PipelineGuard.Crash.Worker;

internal static class WorkerTelemetry
{
    public const string ServiceName = "pipelineguard-crash-worker";
    public const string SourceName = "PipelineGuard.Crash.Worker";
    public const string MeterName = "PipelineGuard.Crash.Worker";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Processed =
        Meter.CreateCounter<long>("pipelineguard.crash.processed", unit: "{report}");

    private static readonly Counter<long> DeliveryFailures =
        Meter.CreateCounter<long>("pipelineguard.crash.delivery.failures", unit: "{delivery}");

    private static readonly Histogram<double> ProcessingDuration =
        Meter.CreateHistogram<double>("pipelineguard.crash.processing.duration", unit: "ms");

    public static void RecordProcessed(string outcome, double milliseconds)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);
        Processed.Add(1, tag);
        ProcessingDuration.Record(milliseconds, tag);
    }

    public static void RecordDeliveryFailure(string reason) =>
        DeliveryFailures.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
