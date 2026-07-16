using PipelineGuard.Contracts;

namespace PipelineGuard.Core.Tests;

public sealed class JsonReportSerializerTests
{
    [Fact]
    public void Serialize_IsDeterministicAcrossInputFindingOrder()
    {
        ValidationFinding first = TestReports.Finding(
            id: "finding-a",
            assetPath: "/Game/A");
        ValidationFinding second = TestReports.Finding(
            id: "finding-b",
            assetPath: "/Game/B");
        ValidationReport forward = TestReports.Report(first, second);
        ValidationReport reverse = forward with { Findings = [second, first] };

        string forwardJson = JsonReportSerializer.Serialize(forward, indented: false);
        string reverseJson = JsonReportSerializer.Serialize(reverse, indented: false);

        Assert.Equal(forwardJson, reverseJson);
    }

    [Fact]
    public void Normalize_SortsByAssetRuleMessageAndId()
    {
        ValidationFinding z = TestReports.Finding("finding-z", assetPath: "/Game/Z");
        ValidationFinding aSecond = TestReports.Finding(
            "finding-b",
            assetPath: "/Game/A",
            ruleId: "PG.ZRule");
        ValidationFinding aFirst = TestReports.Finding(
            "finding-a",
            assetPath: "/Game/A",
            ruleId: "PG.ARule");
        ValidationReport report = TestReports.Report(z, aSecond, aFirst);

        ValidationReport normalized = JsonReportSerializer.Normalize(report);

        Assert.Equal(
            ["finding-a", "finding-b", "finding-z"],
            normalized.Findings.Select(item => item.Id));
    }

    [Fact]
    public void Normalize_DoesNotMutateInputFindingOrder()
    {
        ValidationReport report = TestReports.Report(
            TestReports.Finding("finding-z", assetPath: "/Game/Z"),
            TestReports.Finding("finding-a", assetPath: "/Game/A"));

        _ = JsonReportSerializer.Normalize(report);

        Assert.Equal(
            ["finding-z", "finding-a"],
            report.Findings.Select(item => item.Id));
    }

    [Fact]
    public void Serialize_OmitsNullOptionalProperties()
    {
        ValidationReport report = TestReports.Report(TestReports.Finding());

        string json = JsonReportSerializer.Serialize(report, indented: false);

        Assert.DoesNotContain("$schema", json, StringComparison.Ordinal);
        Assert.DoesNotContain("suppressionReason", json, StringComparison.Ordinal);
        Assert.DoesNotContain("documentationUri", json, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WritesSeverityAsLowercaseString()
    {
        ValidationReport report = TestReports.Report(TestReports.Finding(
            severity: ValidationSeverity.Error));

        string json = JsonReportSerializer.Serialize(report, indented: false);

        Assert.Contains("\"severity\":\"error\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_CompactModeDoesNotAddLineBreaks()
    {
        string json = JsonReportSerializer.Serialize(TestReports.Report(), indented: false);

        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain('\r', json);
    }

    [Fact]
    public async Task WriteAndReadFileAsync_RoundTripsReport()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"PipelineGuard-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "report.json");
        ValidationReport expected = TestReports.Report(TestReports.Finding());

        try
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            await JsonReportSerializer.WriteFileAsync(path, expected, cancellationToken);
            ValidationReport actual = await JsonReportSerializer.ReadFileAsync(path, cancellationToken);

            Assert.Equal(expected.RunId, actual.RunId);
            Assert.Equal(expected.Findings[0].Id, actual.Findings[0].Id);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
