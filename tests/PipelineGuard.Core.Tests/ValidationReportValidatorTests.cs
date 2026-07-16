using PipelineGuard.Contracts;

namespace PipelineGuard.Core.Tests;

public sealed class ValidationReportValidatorTests
{
    [Fact]
    public void Validate_AcceptsConformingReport()
    {
        ValidationReport report = TestReports.Report(
            TestReports.Finding("error-1", ValidationSeverity.Error),
            TestReports.Finding("warning-1", ValidationSeverity.Warning),
            TestReports.Finding("suppressed-1", suppressed: true));

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RequiresReport()
    {
        IReadOnlyList<string> errors = ValidationReportValidator.Validate(null);

        Assert.Contains("Report is required.", errors);
    }

    [Fact]
    public void Validate_RejectsUnsupportedSchemaVersion()
    {
        ValidationReport report = TestReports.Report() with { SchemaVersion = "2.0" };

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsEmptyRunId()
    {
        ValidationReport report = TestReports.Report() with { RunId = Guid.Empty };

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("runId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsCompletionBeforeStart()
    {
        ValidationReport report = TestReports.Report() with
        {
            CompletedUtc = DateTimeOffset.Parse("2026-07-16T11:59:59Z")
        };

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("completedUtc", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateScopeEntries()
    {
        ValidationReport report = TestReports.Report() with { Scope = ["/Game", "/Game"] };

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("duplicate entry", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RequiresSuppressionReasonForSuppressedFinding()
    {
        ValidationFinding finding = TestReports.Finding("suppressed-1", suppressed: true) with
        {
            SuppressionReason = null
        };
        ValidationReport report = TestReports.Report(finding);

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("suppressionReason is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsSuppressionReasonOnActiveFinding()
    {
        ValidationFinding finding = TestReports.Finding() with
        {
            SuppressionReason = "This finding is not marked as suppressed."
        };
        ValidationReport report = TestReports.Report(finding);

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("only allowed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateFindingIds()
    {
        ValidationFinding first = TestReports.Finding("duplicate-id", assetPath: "/Game/A");
        ValidationFinding second = TestReports.Finding("duplicate-id", assetPath: "/Game/B");
        ValidationReport report = TestReports.Report(first, second);

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("duplicates", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInvalidFindingIdFormat()
    {
        ValidationReport report = TestReports.Report(TestReports.Finding("contains spaces"));

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("id has an invalid format", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsSummaryCountMismatch()
    {
        ValidationReport report = TestReports.Report(TestReports.Finding(
            severity: ValidationSeverity.Warning));
        report = report with { Summary = report.Summary with { Warnings = 0 } };

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("summary.warnings", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsNonHttpDocumentationUri()
    {
        ValidationFinding finding = TestReports.Finding() with
        {
            DocumentationUri = "file:///C:/private/rule.html"
        };
        ValidationReport report = TestReports.Report(finding);

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("absolute HTTP(S)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInvalidSeverityOnSuppressedFinding()
    {
        ValidationFinding finding = TestReports.Finding(
            "suppressed-invalid",
            (ValidationSeverity)99,
            suppressed: true);
        ValidationReport report = TestReports.Report(finding);

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("severity is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsMoreThanTwentyMetadataEntries()
    {
        IReadOnlyDictionary<string, string> metadata = Enumerable.Range(1, 21)
            .ToDictionary(index => $"key-{index}", index => $"value-{index}", StringComparer.Ordinal);
        ValidationFinding finding = TestReports.Finding() with { Metadata = metadata };
        ValidationReport report = TestReports.Report(finding);

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("more than 20", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsNegativeSummaryValues()
    {
        ValidationReport report = TestReports.Report() with
        {
            Summary = TestReports.Report().Summary with
            {
                AssetsScanned = -1,
                RulesRun = -1,
                DurationMs = -1
            }
        };

        IReadOnlyList<string> errors = ValidationReportValidator.Validate(report);

        Assert.Contains(errors, error => error.Contains("assetsScanned", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("rulesRun", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("durationMs", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAndThrow_ExposesEveryValidationError()
    {
        ValidationReport report = TestReports.Report() with
        {
            SchemaVersion = "0.0",
            RunId = Guid.Empty
        };

        ValidationReportValidationException exception = Assert.Throws<ValidationReportValidationException>(
            () => ValidationReportValidator.ValidateAndThrow(report));

        Assert.True(exception.Errors.Count >= 2);
        Assert.Contains("schemaVersion", exception.Message, StringComparison.Ordinal);
        Assert.Contains("runId", exception.Message, StringComparison.Ordinal);
    }
}
