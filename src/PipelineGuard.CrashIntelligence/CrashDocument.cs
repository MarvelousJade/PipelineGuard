namespace PipelineGuard.CrashIntelligence;

public sealed record CrashDocument
{
    public Guid CrashId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public DateTimeOffset ProcessedAtUtc { get; init; }

    public string Project { get; init; } = string.Empty;

    public CrashBuild Build { get; init; } = new();

    public string Platform { get; init; } = string.Empty;

    public string ErrorType { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> StackTrace { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NormalizedStackTrace { get; init; } = Array.Empty<string>();

    public string Fingerprint { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public static CrashDocument FromEnvelope(CrashEnvelope envelope, DateTimeOffset processedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        CrashReportValidator.Validate(envelope.Report);

        IReadOnlyList<string> normalized = StackTraceNormalizer.Normalize(envelope.Report.StackTrace);
        if (normalized.Count == 0)
        {
            throw new CrashReportValidationException(
                new[] { "stackTrace must contain at least one non-plumbing frame." });
        }

        return new CrashDocument
        {
            CrashId = envelope.Report.CrashId,
            OccurredAtUtc = envelope.Report.OccurredAtUtc,
            ReceivedAtUtc = envelope.ReceivedAtUtc,
            ProcessedAtUtc = processedAtUtc,
            Project = envelope.Report.Project.Trim(),
            Build = envelope.Report.Build,
            Platform = envelope.Report.Platform.Trim(),
            ErrorType = envelope.Report.ErrorType.Trim(),
            ErrorMessage = envelope.Report.ErrorMessage.Trim(),
            StackTrace = envelope.Report.StackTrace,
            NormalizedStackTrace = normalized,
            Fingerprint = CrashFingerprint.Compute(envelope.Report.ErrorType, normalized),
            Metadata = envelope.Report.Metadata
        };
    }
}
