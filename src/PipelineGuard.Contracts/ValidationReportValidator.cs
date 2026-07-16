using System.Text.RegularExpressions;

namespace PipelineGuard.Contracts;

public static partial class ValidationReportValidator
{
    public static IReadOnlyList<string> Validate(ValidationReport? report)
    {
        var errors = new List<string>();
        if (report is null)
        {
            errors.Add("Report is required.");
            return errors;
        }

        if (!string.Equals(
                report.SchemaVersion,
                ValidationReport.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            errors.Add($"schemaVersion must be '{ValidationReport.CurrentSchemaVersion}'.");
        }

        if (report.RunId == Guid.Empty)
        {
            errors.Add("runId must not be empty.");
        }
        if (report.SchemaUri is not null &&
            !Uri.TryCreate(report.SchemaUri, UriKind.RelativeOrAbsolute, out _))
        {
            errors.Add("$schema must be a URI reference.");
        }

        ValidateRequiredText(report.ToolVersion, "toolVersion", 64, errors);
        ValidateRequiredText(report.Project, "project", 128, errors);
        ValidateRequiredText(report.EngineVersion, "engineVersion", 64, errors);

        if (string.IsNullOrWhiteSpace(report.Profile) || !ProfilePattern().IsMatch(report.Profile))
        {
            errors.Add("profile must match ^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$.");
        }

        if (report.CompletedUtc < report.StartedUtc)
        {
            errors.Add("completedUtc must not be earlier than startedUtc.");
        }

        if (report.Scope is null || report.Scope.Count is < 1 or > 32)
        {
            errors.Add("scope must contain between 1 and 32 entries.");
        }
        else
        {
            var uniqueScopes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string scope in report.Scope)
            {
                ValidateRequiredText(scope, "scope entry", 512, errors);
                if (!uniqueScopes.Add(scope))
                {
                    errors.Add($"scope contains duplicate entry '{scope}'.");
                }
            }
        }

        ValidateSummary(report.Summary, errors);
        ValidateFindings(report.Findings, report.Summary, errors);
        return errors;
    }

    public static void ValidateAndThrow(ValidationReport? report)
    {
        IReadOnlyList<string> errors = Validate(report);
        if (errors.Count > 0)
        {
            throw new ValidationReportValidationException(errors);
        }
    }

    private static void ValidateSummary(ValidationSummary? summary, List<string> errors)
    {
        if (summary is null)
        {
            errors.Add("summary is required.");
            return;
        }

        if (summary.AssetsScanned < 0)
        {
            errors.Add("summary.assetsScanned must be non-negative.");
        }
        if (summary.RulesRun < 0)
        {
            errors.Add("summary.rulesRun must be non-negative.");
        }
        if (summary.Errors < 0 || summary.Warnings < 0 || summary.Infos < 0)
        {
            errors.Add("summary severity counts must be non-negative.");
        }
        if (summary.Suppressed < 0)
        {
            errors.Add("summary.suppressed must be non-negative.");
        }
        if (summary.DurationMs < 0)
        {
            errors.Add("summary.durationMs must be non-negative.");
        }
    }

    private static void ValidateFindings(
        IReadOnlyList<ValidationFinding>? findings,
        ValidationSummary? summary,
        List<string> errors)
    {
        if (findings is null)
        {
            errors.Add("findings is required.");
            return;
        }
        if (findings.Count > 20_000)
        {
            errors.Add("findings must not exceed 20000 entries.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        int actualErrors = 0;
        int actualWarnings = 0;
        int actualInfos = 0;
        int actualSuppressed = 0;

        for (int index = 0; index < findings.Count; index++)
        {
            ValidationFinding? finding = findings[index];
            if (finding is null)
            {
                errors.Add($"findings[{index}] must not be null.");
                continue;
            }

            string prefix = $"findings[{index}]";
            if (string.IsNullOrWhiteSpace(finding.Id) || !FindingIdPattern().IsMatch(finding.Id))
            {
                errors.Add($"{prefix}.id has an invalid format.");
            }
            else if (!ids.Add(finding.Id))
            {
                errors.Add($"{prefix}.id duplicates '{finding.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(finding.RuleId) || !RuleIdPattern().IsMatch(finding.RuleId))
            {
                errors.Add($"{prefix}.ruleId has an invalid format.");
            }

            ValidateRequiredText(finding.AssetPath, $"{prefix}.assetPath", 512, errors);
            ValidateRequiredText(finding.Message, $"{prefix}.message", 2048, errors);
            ValidateRequiredText(finding.Remediation, $"{prefix}.remediation", 2048, errors);

            bool hasValidSeverity = Enum.IsDefined(finding.Severity);
            if (!hasValidSeverity)
            {
                errors.Add($"{prefix}.severity is invalid.");
            }

            if (finding.Suppressed)
            {
                actualSuppressed++;
                ValidateRequiredText(
                    finding.SuppressionReason,
                    $"{prefix}.suppressionReason",
                    512,
                    errors);
            }
            else
            {
                if (finding.SuppressionReason is not null)
                {
                    errors.Add($"{prefix}.suppressionReason is only allowed for suppressed findings.");
                }

                if (hasValidSeverity)
                {
                    switch (finding.Severity)
                    {
                        case ValidationSeverity.Error:
                            actualErrors++;
                            break;
                        case ValidationSeverity.Warning:
                            actualWarnings++;
                            break;
                        case ValidationSeverity.Info:
                            actualInfos++;
                            break;
                    }
                }
            }

            if (finding.DocumentationUri is not null)
            {
                if (finding.DocumentationUri.Length > 2048)
                {
                    errors.Add($"{prefix}.documentationUri exceeds 2048 characters.");
                }
                if (!Uri.TryCreate(finding.DocumentationUri, UriKind.Absolute, out Uri? uri) ||
                    uri.Scheme is not ("http" or "https"))
                {
                    errors.Add($"{prefix}.documentationUri must be an absolute HTTP(S) URI.");
                }
            }

            if (finding.Metadata is { Count: > 20 })
            {
                errors.Add($"{prefix}.metadata must not contain more than 20 entries.");
            }
            if (finding.Metadata is not null)
            {
                foreach ((string key, string value) in finding.Metadata)
                {
                    ValidateRequiredText(key, $"{prefix}.metadata key", 512, errors);
                    if (value is null)
                    {
                        errors.Add($"{prefix}.metadata['{key}'] must be a string.");
                    }
                    else if (value.Length > 512)
                    {
                        errors.Add($"{prefix}.metadata['{key}'] exceeds 512 characters.");
                    }
                }
            }
        }

        if (summary is null)
        {
            return;
        }
        if (summary.Errors != actualErrors)
        {
            errors.Add($"summary.errors is {summary.Errors}, but findings contain {actualErrors} unsuppressed errors.");
        }
        if (summary.Warnings != actualWarnings)
        {
            errors.Add($"summary.warnings is {summary.Warnings}, but findings contain {actualWarnings} unsuppressed warnings.");
        }
        if (summary.Infos != actualInfos)
        {
            errors.Add($"summary.infos is {summary.Infos}, but findings contain {actualInfos} unsuppressed infos.");
        }
        if (summary.Suppressed != actualSuppressed)
        {
            errors.Add($"summary.suppressed is {summary.Suppressed}, but findings contain {actualSuppressed} suppressed findings.");
        }
    }

    private static void ValidateRequiredText(
        string? value,
        string field,
        int maxLength,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} is required.");
        }
        else if (value.Length > maxLength)
        {
            errors.Add($"{field} must not exceed {maxLength} characters.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfilePattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex FindingIdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex RuleIdPattern();
}
