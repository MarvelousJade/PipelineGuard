namespace PipelineGuard.CrashIntelligence;

public sealed record CrashEnvelope
{
    public string EventVersion { get; init; } = "1.0";

    public Guid MessageId { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public UnrealCrashReport Report { get; init; } = new();

    public static CrashEnvelope Create(UnrealCrashReport report, DateTimeOffset receivedAtUtc) => new()
    {
        MessageId = report.CrashId,
        ReceivedAtUtc = receivedAtUtc,
        Report = report
    };
}
