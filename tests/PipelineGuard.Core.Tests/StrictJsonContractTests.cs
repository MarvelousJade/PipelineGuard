using System.Text.Json;
using System.Text.Json.Nodes;
using PipelineGuard.Contracts;

namespace PipelineGuard.Core.Tests;

public sealed class StrictJsonContractTests
{
    [Fact]
    public void Deserialize_AcceptsConformingReport()
    {
        ValidationReport expected = TestReports.Report(TestReports.Finding());

        ValidationReport actual = JsonReportSerializer.Deserialize(
            JsonReportSerializer.Serialize(expected, indented: false));

        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.Project, actual.Project);
        Assert.Single(actual.Findings);
    }

    [Fact]
    public void Deserialize_AcceptsOptionalSchemaUri()
    {
        ValidationReport report = TestReports.Report() with
        {
            SchemaUri = "https://example.test/pipelineguard-report-v1.schema.json"
        };

        ValidationReport actual = JsonReportSerializer.Deserialize(
            JsonReportSerializer.Serialize(report, indented: false));

        Assert.Equal(report.SchemaUri, actual.SchemaUri);
    }

    [Fact]
    public void Deserialize_RejectsUnknownTopLevelProperty()
    {
        JsonObject json = ParseReport(TestReports.Report());
        json["unexpected"] = true;

        Assert.Throws<JsonException>(() => JsonReportSerializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Deserialize_RejectsUnknownFindingProperty()
    {
        JsonObject json = ParseReport(TestReports.Report(TestReports.Finding()));
        json["findings"]!.AsArray()[0]!.AsObject()["unexpected"] = true;

        Assert.Throws<JsonException>(() => JsonReportSerializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Deserialize_RejectsNumericSeverity()
    {
        JsonObject json = ParseReport(TestReports.Report(TestReports.Finding()));
        json["findings"]!.AsArray()[0]!.AsObject()["severity"] = 2;

        Assert.Throws<JsonException>(() => JsonReportSerializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Deserialize_UsesCaseSensitivePropertyNames()
    {
        JsonObject json = ParseReport(TestReports.Report());
        JsonNode? project = json["project"];
        json.Remove("project");
        json["Project"] = project;

        Assert.Throws<JsonException>(() => JsonReportSerializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Deserialize_RejectsMissingRequiredProperty()
    {
        JsonObject json = ParseReport(TestReports.Report());
        json.Remove("toolVersion");

        Assert.Throws<JsonException>(() => JsonReportSerializer.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Deserialize_RejectsSchemaValidJsonWithInvalidSummaryTotals()
    {
        JsonObject json = ParseReport(TestReports.Report(TestReports.Finding(
            severity: ValidationSeverity.Error)));
        json["summary"]!.AsObject()["errors"] = 0;

        ValidationReportValidationException exception = Assert.Throws<ValidationReportValidationException>(
            () => JsonReportSerializer.Deserialize(json.ToJsonString()));

        Assert.Contains(exception.Errors, error => error.Contains("summary.errors", StringComparison.Ordinal));
    }

    private static JsonObject ParseReport(ValidationReport report)
    {
        return JsonNode.Parse(JsonReportSerializer.Serialize(report, indented: false))!.AsObject();
    }
}
