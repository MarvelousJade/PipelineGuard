using System.Text.Json.Serialization;

namespace PipelineGuard.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ValidationFinding
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required ValidationSeverity Severity { get; init; }
    public required string AssetPath { get; init; }
    public required string Message { get; init; }
    public required string Remediation { get; init; }
    public required bool Suppressed { get; init; }
    public string? SuppressionReason { get; init; }
    public string? DocumentationUri { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
