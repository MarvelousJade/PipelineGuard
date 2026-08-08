namespace PipelineGuard.CrashIntelligence;

public static class CrashReportValidator
{
    public const int MaxFrames = 256;
    public const int MaxMetadataEntries = 32;

    public static void Validate(UnrealCrashReport? report)
    {
        var errors = new List<string>();
        if (report is null)
        {
            throw new CrashReportValidationException(new[] { "A request body is required." });
        }

        if (!string.Equals(report.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            errors.Add("schemaVersion must be '1.0'.");
        }

        if (report.CrashId == Guid.Empty)
        {
            errors.Add("crashId must be a non-empty UUID.");
        }

        if (report.OccurredAtUtc == default)
        {
            errors.Add("occurredAtUtc is required.");
        }
        else if (report.OccurredAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            errors.Add("occurredAtUtc cannot be more than five minutes in the future.");
        }

        RequireBounded(report.Project, "project", 1, 100, errors);
        RequireBounded(report.Build?.Version, "build.version", 1, 100, errors);
        RequireBounded(report.Build?.Branch, "build.branch", 1, 200, errors);
        RequireBounded(report.Platform, "platform", 1, 50, errors);
        RequireBounded(report.ErrorType, "errorType", 1, 100, errors);
        RequireBounded(report.ErrorMessage, "errorMessage", 1, 4_096, errors);

        if (report.StackTrace is null || report.StackTrace.Count == 0)
        {
            errors.Add("stackTrace must contain at least one frame.");
        }
        else
        {
            if (report.StackTrace.Count > MaxFrames)
            {
                errors.Add($"stackTrace cannot contain more than {MaxFrames} frames.");
            }

            for (int index = 0; index < report.StackTrace.Count; index++)
            {
                RequireBounded(report.StackTrace[index], $"stackTrace[{index}]", 1, 2_048, errors);
            }
        }

        if (report.Metadata is { Count: > MaxMetadataEntries })
        {
            errors.Add($"metadata cannot contain more than {MaxMetadataEntries} entries.");
        }

        if (report.Metadata is not null)
        {
            foreach ((string key, string value) in report.Metadata)
            {
                RequireBounded(key, "metadata key", 1, 100, errors);
                RequireBounded(value, $"metadata['{key}']", 0, 500, errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new CrashReportValidationException(errors);
        }
    }

    private static void RequireBounded(
        string? value,
        string name,
        int minimumLength,
        int maximumLength,
        ICollection<string> errors)
    {
        int length = value?.Trim().Length ?? 0;
        if (length < minimumLength || length > maximumLength)
        {
            errors.Add($"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }
}
