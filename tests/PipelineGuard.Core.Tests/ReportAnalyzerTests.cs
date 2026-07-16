using PipelineGuard.Contracts;

namespace PipelineGuard.Core.Tests;

public sealed class ReportAnalyzerTests
{
    [Fact]
    public void GetTotals_UsesReportSummaryCounts()
    {
        ValidationReport report = TestReports.Report(
            TestReports.Finding("info-1"),
            TestReports.Finding("warning-1", ValidationSeverity.Warning),
            TestReports.Finding("error-1", ValidationSeverity.Error));

        SeverityTotals totals = ReportAnalyzer.GetTotals(report);

        Assert.Equal(new SeverityTotals(1, 1, 1), totals);
        Assert.Equal(3, totals.Total);
        Assert.Equal(ValidationSeverity.Error, totals.HighestSeverity);
    }

    [Fact]
    public void Filter_ExcludesSuppressedFindingsByDefault()
    {
        ValidationReport report = TestReports.Report(
            TestReports.Finding("active-error", ValidationSeverity.Error),
            TestReports.Finding("suppressed-error", ValidationSeverity.Error, suppressed: true));

        IReadOnlyList<ValidationFinding> findings = ReportAnalyzer.Filter(
            report,
            ValidationSeverity.Info);

        ValidationFinding finding = Assert.Single(findings);
        Assert.Equal("active-error", finding.Id);
    }

    [Fact]
    public void Filter_CanIncludeSuppressedFindings()
    {
        ValidationReport report = TestReports.Report(
            TestReports.Finding("active-error", ValidationSeverity.Error),
            TestReports.Finding("suppressed-error", ValidationSeverity.Error, suppressed: true));

        IReadOnlyList<ValidationFinding> findings = ReportAnalyzer.Filter(
            report,
            ValidationSeverity.Info,
            includeSuppressed: true);

        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Filter_AppliesMinimumSeverityAndDeterministicOrder()
    {
        ValidationReport report = TestReports.Report(
            TestReports.Finding("warning-z", ValidationSeverity.Warning, "/Game/Z"),
            TestReports.Finding("error-z", ValidationSeverity.Error, "/Game/Z"),
            TestReports.Finding("info-a", ValidationSeverity.Info, "/Game/A"),
            TestReports.Finding("warning-a", ValidationSeverity.Warning, "/Game/A"));

        IReadOnlyList<ValidationFinding> findings = ReportAnalyzer.Filter(
            report,
            ValidationSeverity.Warning);

        Assert.Equal(
            ["error-z", "warning-a", "warning-z"],
            findings.Select(item => item.Id));
    }

    [Theory]
    [InlineData(ValidationSeverity.Info, true)]
    [InlineData(ValidationSeverity.Warning, true)]
    [InlineData(ValidationSeverity.Error, false)]
    public void MeetsFailureThreshold_UsesInclusiveSeverity(
        ValidationSeverity threshold,
        bool expected)
    {
        ValidationReport report = TestReports.Report(TestReports.Finding(
            severity: ValidationSeverity.Warning));

        bool actual = ReportAnalyzer.MeetsFailureThreshold(report, threshold);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MeetsFailureThreshold_IgnoresSuppressedFindings()
    {
        ValidationReport report = TestReports.Report(TestReports.Finding(
            severity: ValidationSeverity.Error,
            suppressed: true));

        bool actual = ReportAnalyzer.MeetsFailureThreshold(
            report,
            ValidationSeverity.Info);

        Assert.False(actual);
    }

    [Fact]
    public void Compare_ClassifiesAddedResolvedAndPersistingFindings()
    {
        ValidationReport baseline = TestReports.Report(
            TestReports.Finding("resolved"),
            TestReports.Finding("persistent"));
        ValidationReport current = TestReports.Report(
            TestReports.Finding("persistent"),
            TestReports.Finding("added"));

        ReportComparison comparison = ReportAnalyzer.Compare(baseline, current);

        Assert.Equal("added", Assert.Single(comparison.Added).Id);
        Assert.Equal("resolved", Assert.Single(comparison.Resolved).Id);
        Assert.Equal("persistent", Assert.Single(comparison.Persisting).Current.Id);
    }

    [Fact]
    public void Compare_OrdersAddedAndResolvedByFindingId()
    {
        ValidationReport baseline = TestReports.Report(
            TestReports.Finding("resolved-z"),
            TestReports.Finding("resolved-a"));
        ValidationReport current = TestReports.Report(
            TestReports.Finding("added-z"),
            TestReports.Finding("added-a"));

        ReportComparison comparison = ReportAnalyzer.Compare(baseline, current);

        Assert.Equal(["added-a", "added-z"], comparison.Added.Select(item => item.Id));
        Assert.Equal(["resolved-a", "resolved-z"], comparison.Resolved.Select(item => item.Id));
    }

    [Fact]
    public void Compare_CountsSeverityAndSuppressionChanges()
    {
        ValidationReport baseline = TestReports.Report(
            TestReports.Finding("severity-change", ValidationSeverity.Info),
            TestReports.Finding("suppression-change", ValidationSeverity.Warning));
        ValidationReport current = TestReports.Report(
            TestReports.Finding("severity-change", ValidationSeverity.Error),
            TestReports.Finding("suppression-change", ValidationSeverity.Warning, suppressed: true));

        ReportComparison comparison = ReportAnalyzer.Compare(baseline, current);

        Assert.Equal(2, comparison.ChangedCount);
        Assert.All(comparison.Persisting, item =>
            Assert.True(item.SeverityChanged || item.SuppressionChanged));
    }
}
