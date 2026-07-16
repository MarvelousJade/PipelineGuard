using PipelineGuard.Contracts;

namespace PipelineGuard.Core.Tests;

internal static class TestReports
{
    private static readonly Guid DefaultRunId = Guid.Parse("9d9566f5-4941-4d80-8028-e51b27b18945");

    public static ValidationFinding Finding(
        string id = "finding-1",
        ValidationSeverity severity = ValidationSeverity.Info,
        string assetPath = "/Game/Characters/Hero",
        string ruleId = "PG.Asset.Valid",
        string message = "Asset passed the requested validation rule.",
        bool suppressed = false)
    {
        return new ValidationFinding
        {
            Id = id,
            RuleId = ruleId,
            Severity = severity,
            AssetPath = assetPath,
            Message = message,
            Remediation = "Update the asset so it satisfies the rule.",
            Suppressed = suppressed,
            SuppressionReason = suppressed ? "Accepted for this test fixture." : null
        };
    }

    public static ValidationReport Report(params ValidationFinding[] findings)
    {
        return Report((IReadOnlyList<ValidationFinding>)findings);
    }

    public static ValidationReport Report(IReadOnlyList<ValidationFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return new ValidationReport
        {
            SchemaVersion = ValidationReport.CurrentSchemaVersion,
            RunId = DefaultRunId,
            ToolVersion = "0.1.0",
            StartedUtc = DateTimeOffset.Parse("2026-07-16T12:00:00Z"),
            CompletedUtc = DateTimeOffset.Parse("2026-07-16T12:00:01Z"),
            Project = "BlackWood",
            EngineVersion = "5.6.1",
            Profile = "editor-ci",
            Scope = ["/Game"],
            Summary = SummaryFor(findings),
            Findings = findings
        };
    }

    public static ValidationSummary SummaryFor(IReadOnlyList<ValidationFinding> findings)
    {
        return new ValidationSummary
        {
            AssetsScanned = 12,
            RulesRun = 5,
            Errors = findings.Count(item => !item.Suppressed && item.Severity == ValidationSeverity.Error),
            Warnings = findings.Count(item => !item.Suppressed && item.Severity == ValidationSeverity.Warning),
            Infos = findings.Count(item => !item.Suppressed && item.Severity == ValidationSeverity.Info),
            Suppressed = findings.Count(item => item.Suppressed),
            DurationMs = 1_000
        };
    }
}
