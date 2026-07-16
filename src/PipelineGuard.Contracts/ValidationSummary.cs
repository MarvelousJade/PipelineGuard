using System.Text.Json.Serialization;

namespace PipelineGuard.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ValidationSummary
{
    public required int AssetsScanned { get; init; }
    public required int RulesRun { get; init; }
    public required int Errors { get; init; }
    public required int Warnings { get; init; }
    public required int Infos { get; init; }
    public required int Suppressed { get; init; }
    public required long DurationMs { get; init; }
}

public readonly record struct SeverityTotals(int Infos, int Warnings, int Errors)
{
    public int Total => Infos + Warnings + Errors;

    public ValidationSeverity? HighestSeverity => Errors > 0
        ? ValidationSeverity.Error
        : Warnings > 0
            ? ValidationSeverity.Warning
            : Infos > 0
                ? ValidationSeverity.Info
                : null;
}
