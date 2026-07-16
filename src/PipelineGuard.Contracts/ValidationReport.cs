using System.Text.Json.Serialization;

namespace PipelineGuard.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ValidationReport
{
    public const string CurrentSchemaVersion = "1.0";

    [JsonPropertyName("$schema")]
    public string? SchemaUri { get; init; }

    public required string SchemaVersion { get; init; }
    public required Guid RunId { get; init; }
    public required string ToolVersion { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public required DateTimeOffset CompletedUtc { get; init; }
    public required string Project { get; init; }
    public required string EngineVersion { get; init; }
    public required string Profile { get; init; }
    public required IReadOnlyList<string> Scope { get; init; }
    public required ValidationSummary Summary { get; init; }
    public required IReadOnlyList<ValidationFinding> Findings { get; init; }
}
