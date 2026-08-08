using System.Diagnostics;
using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.Crash.Worker;

public sealed class CrashEventProcessor
{
    private readonly ICrashIndex _index;
    private readonly TimeProvider _timeProvider;

    public CrashEventProcessor(ICrashIndex index, TimeProvider timeProvider)
    {
        _index = index;
        _timeProvider = timeProvider;
    }

    public async Task<CrashIndexWriteOutcome> ProcessAsync(
        CrashEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using Activity? activity = WorkerTelemetry.ActivitySource.StartActivity("crash.process");
        CrashDocument document = CrashDocument.FromEnvelope(
            envelope,
            _timeProvider.GetUtcNow());
        activity?.SetTag("crash.fingerprint", document.Fingerprint);
        activity?.SetTag("crash.project", document.Project);

        CrashIndexWriteOutcome outcome = await _index.TryCreateAsync(document, cancellationToken)
            .ConfigureAwait(false);
        string outcomeName = outcome == CrashIndexWriteOutcome.Created ? "created" : "duplicate";
        WorkerTelemetry.RecordProcessed(outcomeName, stopwatch.Elapsed.TotalMilliseconds);
        return outcome;
    }
}
