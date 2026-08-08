namespace PipelineGuard.CrashIntelligence;

public sealed record UnrealCrashReport
{
    public string SchemaVersion { get; init; } = string.Empty;

    public Guid CrashId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string Project { get; init; } = string.Empty;

    public CrashBuild Build { get; init; } = new();

    public string Platform { get; init; } = string.Empty;

    public string ErrorType { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> StackTrace { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record CrashBuild
{
    public string Version { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public long? Changelist { get; init; }
}
