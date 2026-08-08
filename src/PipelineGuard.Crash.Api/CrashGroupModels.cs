namespace PipelineGuard.Crash.Api;

public sealed record CrashAcceptedResponse(Guid CrashId, string Status, Guid IdempotencyKey);

public sealed record CrashGroupSearchRequest(
    string? Query,
    string? Project,
    string? Build,
    string? Platform,
    string? Status,
    int? Limit);

public sealed record CrashGroupSearchResult(
    long TotalCrashes,
    int ReturnedGroups,
    IReadOnlyList<CrashGroupSummary> Items);

public sealed record CrashGroupSummary(
    string Fingerprint,
    long Occurrences,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    long AffectedBuildCount,
    IReadOnlyList<string> AffectedBuilds,
    string ErrorType,
    string ErrorMessage,
    string RepresentativeFrame,
    string RegressionStatus,
    string? ResolvedInBuild);

public sealed record ResolveCrashGroupRequest(string ResolvedInBuild, string? Note);

public sealed record CrashGroupResolution(
    string Fingerprint,
    DateTimeOffset ResolvedAtUtc,
    string ResolvedInBuild,
    string? Note);
