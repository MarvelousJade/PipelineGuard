using PipelineGuard.Contracts;

namespace PipelineGuard.Core;

public static class ReportAnalyzer
{
    public static SeverityTotals GetTotals(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new SeverityTotals(
            report.Summary.Infos,
            report.Summary.Warnings,
            report.Summary.Errors);
    }

    public static IReadOnlyList<ValidationFinding> Filter(
        ValidationReport report,
        ValidationSeverity minimum,
        bool includeSuppressed = false)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Findings
            .Where(finding =>
                (includeSuppressed || !finding.Suppressed) &&
                (int)finding.Severity >= (int)minimum)
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.AssetPath, StringComparer.Ordinal)
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool MeetsFailureThreshold(
        ValidationReport report,
        ValidationSeverity threshold)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Findings.Any(finding =>
            !finding.Suppressed && (int)finding.Severity >= (int)threshold);
    }

    public static ReportComparison Compare(
        ValidationReport baseline,
        ValidationReport current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        IReadOnlyDictionary<string, ValidationFinding> before = baseline.Findings
            .ToDictionary(finding => finding.Id, StringComparer.Ordinal);
        IReadOnlyDictionary<string, ValidationFinding> after = current.Findings
            .ToDictionary(finding => finding.Id, StringComparer.Ordinal);

        ValidationFinding[] added = after
            .Where(pair => !before.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(finding => finding.Id, StringComparer.Ordinal)
            .ToArray();
        ValidationFinding[] resolved = before
            .Where(pair => !after.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(finding => finding.Id, StringComparer.Ordinal)
            .ToArray();
        PersistingFinding[] persisting = after
            .Where(pair => before.ContainsKey(pair.Key))
            .Select(pair => new PersistingFinding(before[pair.Key], pair.Value))
            .OrderBy(pair => pair.Current.Id, StringComparer.Ordinal)
            .ToArray();

        return new ReportComparison(added, resolved, persisting);
    }
}

public sealed record PersistingFinding(
    ValidationFinding Baseline,
    ValidationFinding Current)
{
    public bool SeverityChanged => Baseline.Severity != Current.Severity;
    public bool SuppressionChanged => Baseline.Suppressed != Current.Suppressed;
}

public sealed record ReportComparison(
    IReadOnlyList<ValidationFinding> Added,
    IReadOnlyList<ValidationFinding> Resolved,
    IReadOnlyList<PersistingFinding> Persisting)
{
    public int ChangedCount => Persisting.Count(item =>
        item.SeverityChanged || item.SuppressionChanged);
}
