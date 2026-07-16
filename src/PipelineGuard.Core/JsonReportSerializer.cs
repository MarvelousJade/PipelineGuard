using System.Text.Json;
using System.Text.Json.Serialization;
using PipelineGuard.Contracts;

namespace PipelineGuard.Core;

public static class JsonReportSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static ValidationReport Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ValidationReport report = JsonSerializer.Deserialize<ValidationReport>(json, SerializerOptions)
            ?? throw new JsonException("The JSON document did not contain a validation report.");
        ValidationReportValidator.ValidateAndThrow(report);
        return report;
    }

    public static ValidationReport ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(File.ReadAllText(path));
    }

    public static async Task<ValidationReport> ReadFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }

    public static string Serialize(ValidationReport report, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidationReport normalized = Normalize(report);
        ValidationReportValidator.ValidateAndThrow(normalized);
        JsonSerializerOptions options = new(SerializerOptions)
        {
            WriteIndented = indented
        };
        return JsonSerializer.Serialize(normalized, options);
    }

    public static async Task WriteFileAsync(
        string path,
        ValidationReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            fullPath,
            Serialize(report) + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);
    }

    public static ValidationReport Normalize(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report with
        {
            Findings = report.Findings
                .OrderBy(finding => finding.AssetPath, StringComparer.Ordinal)
                .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.Message, StringComparer.Ordinal)
                .ThenBy(finding => finding.Id, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            PropertyNameCaseInsensitive = false
        };
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        return options;
    }
}
