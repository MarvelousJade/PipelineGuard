using System.Text.Json;
using PipelineGuard.Contracts;
using PipelineGuard.Core;

return await PipelineGuardCli.RunAsync(args).ConfigureAwait(false);

internal static class PipelineGuardCli
{
    private const int Clean = 0;
    private const int ValidationFailed = 1;
    private const int InvalidInput = 2;
    private const int ExecutionFailed = 3;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintRootHelp();
            return Clean;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "check" => Check(args[1..]),
                "summary" => Summary(args[1..]),
                "compare" => Compare(args[1..]),
                "validate" => await ValidateAsync(args[1..]).ConfigureAwait(false),
                _ => throw new CliUsageException($"Unknown command '{args[0]}'.")
            };
        }
        catch (CliUsageException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            Console.Error.WriteLine("Run 'PipelineGuard.Cli --help' for usage.");
            return InvalidInput;
        }
        catch (ValidationReportValidationException exception)
        {
            Console.Error.WriteLine($"invalid report: {exception.Message}");
            return InvalidInput;
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"invalid JSON: {exception.Message}");
            return InvalidInput;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("operation cancelled");
            return ExecutionFailed;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or TimeoutException or InvalidOperationException)
        {
            Console.Error.WriteLine($"execution failed: {exception.Message}");
            return ExecutionFailed;
        }
    }

    private static int Check(string[] args)
    {
        if (args.Length > 0 && IsHelp(args[0]))
        {
            PrintReportHelp("check");
            return Clean;
        }

        ParsedArguments parsed = ParsedArguments.Parse(args, 1, "--fail-on");
        ValidationSeverity failureThreshold = parsed.GetSeverity("--fail-on", ValidationSeverity.Error);
        ValidationReport report = JsonReportSerializer.ReadFile(parsed.Positionals[0]);
        bool failed = ReportAnalyzer.MeetsFailureThreshold(report, failureThreshold);
        Console.WriteLine(failed ? "validation failed" : "validation passed");
        return failed ? ValidationFailed : Clean;
    }

    private static int Summary(string[] args)
    {
        if (args.Length > 0 && IsHelp(args[0]))
        {
            PrintReportHelp("summary");
            return Clean;
        }

        ParsedArguments parsed = ParsedArguments.Parse(args, 1, "--minimum", "--fail-on");
        ValidationSeverity minimum = parsed.GetSeverity("--minimum", ValidationSeverity.Info);
        ValidationSeverity failureThreshold = parsed.GetSeverity("--fail-on", ValidationSeverity.Error);
        ValidationReport report = JsonReportSerializer.ReadFile(parsed.Positionals[0]);
        IReadOnlyList<ValidationFinding> findings = ReportAnalyzer.Filter(report, minimum);

        Console.WriteLine($"Run {report.RunId:D} | {report.Project} | {report.Profile}");
        Console.WriteLine(
            $"Scanned {report.Summary.AssetsScanned} assets with {report.Summary.RulesRun} rules in {report.Summary.DurationMs} ms");
        Console.WriteLine(
            $"Errors {report.Summary.Errors} | Warnings {report.Summary.Warnings} | Info {report.Summary.Infos} | Suppressed {report.Summary.Suppressed}");
        foreach (ValidationFinding finding in findings)
        {
            Console.WriteLine(
                $"[{finding.Severity.ToString().ToUpperInvariant()}] {finding.RuleId} {finding.AssetPath}: {finding.Message}");
        }

        return ReportAnalyzer.MeetsFailureThreshold(report, failureThreshold)
            ? ValidationFailed
            : Clean;
    }

    private static int Compare(string[] args)
    {
        if (args.Length > 0 && IsHelp(args[0]))
        {
            Console.WriteLine("Usage: PipelineGuard.Cli compare <baseline.json> <current.json> [--fail-on info|warning|error]");
            return Clean;
        }

        ParsedArguments parsed = ParsedArguments.Parse(args, 2, "--fail-on");
        ValidationSeverity failureThreshold = parsed.GetSeverity("--fail-on", ValidationSeverity.Error);
        ValidationReport baseline = JsonReportSerializer.ReadFile(parsed.Positionals[0]);
        ValidationReport current = JsonReportSerializer.ReadFile(parsed.Positionals[1]);
        ReportComparison comparison = ReportAnalyzer.Compare(baseline, current);

        Console.WriteLine(
            $"Added {comparison.Added.Count} | Resolved {comparison.Resolved.Count} | Persisting {comparison.Persisting.Count} | Changed {comparison.ChangedCount}");
        foreach (ValidationFinding finding in comparison.Added)
        {
            Console.WriteLine($"+ [{finding.Severity}] {finding.RuleId} {finding.AssetPath}");
        }
        foreach (ValidationFinding finding in comparison.Resolved)
        {
            Console.WriteLine($"- [{finding.Severity}] {finding.RuleId} {finding.AssetPath}");
        }

        return ReportAnalyzer.MeetsFailureThreshold(current, failureThreshold)
            ? ValidationFailed
            : Clean;
    }

    private static async Task<int> ValidateAsync(string[] args)
    {
        if (args.Length > 0 && IsHelp(args[0]))
        {
            Console.WriteLine("Usage: PipelineGuard.Cli validate --editor <UnrealEditor-Cmd.exe> --project <Project.uproject> --report <report.json> [--fail-on info|warning|error] [--timeout-seconds 1..7200]");
            return Clean;
        }

        ParsedArguments parsed = ParsedArguments.Parse(
            args,
            0,
            "--editor",
            "--project",
            "--report",
            "--fail-on",
            "--timeout-seconds");
        string editorPath = Path.GetFullPath(parsed.GetRequired("--editor"));
        string projectPath = Path.GetFullPath(parsed.GetRequired("--project"));
        string reportPath = Path.GetFullPath(parsed.GetRequired("--report"));
        ValidationSeverity failureThreshold = parsed.GetSeverity("--fail-on", ValidationSeverity.Error);
        int timeoutSeconds = parsed.GetInt32("--timeout-seconds", 900, 1, 7200);

        var runner = new UnrealValidationRunner();
        UnrealValidationResult result = await runner.RunAsync(
            editorPath,
            projectPath,
            reportPath,
            failureThreshold,
            TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            Console.Write(result.StandardOutput);
        }
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Console.Error.Write(result.StandardError);
        }
        Console.WriteLine($"Unreal exit {result.RawExitCode}; PipelineGuard exit {result.ExitCode}; report {reportPath}");
        return result.ExitCode;
    }

    private static bool IsHelp(string value) => value is "--help" or "-h" or "help";

    private static void PrintRootHelp()
    {
        Console.WriteLine("PipelineGuard - deterministic Unreal content validation");
        Console.WriteLine("Commands:");
        Console.WriteLine("  check <report.json>       Validate a report and evaluate a failure threshold");
        Console.WriteLine("  summary <report.json>     Print report totals and findings");
        Console.WriteLine("  compare <old> <new>       Compare findings by stable ID");
        Console.WriteLine("  validate [options]        Run the Unreal commandlet and inspect its report");
        Console.WriteLine("Run any command with --help for details.");
    }

    private static void PrintReportHelp(string command)
    {
        Console.WriteLine($"Usage: PipelineGuard.Cli {command} <report.json> [--fail-on info|warning|error]" +
            (command == "summary" ? " [--minimum info|warning|error]" : string.Empty));
    }
}

internal sealed class ParsedArguments
{
    private readonly IReadOnlyDictionary<string, string> options;

    private ParsedArguments(IReadOnlyList<string> positionals, IReadOnlyDictionary<string, string> options)
    {
        Positionals = positionals;
        this.options = options;
    }

    public IReadOnlyList<string> Positionals { get; }

    public static ParsedArguments Parse(
        string[] args,
        int requiredPositionals,
        params string[] allowedOptions)
    {
        var allowed = new HashSet<string>(allowedOptions, StringComparer.Ordinal);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var positionals = new List<string>();

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(argument);
                continue;
            }

            if (!allowed.Contains(argument))
            {
                throw new CliUsageException($"Unknown option '{argument}'.");
            }
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException($"Option '{argument}' requires a value.");
            }
            if (!options.TryAdd(argument, args[++index]))
            {
                throw new CliUsageException($"Option '{argument}' was specified more than once.");
            }
        }

        if (positionals.Count != requiredPositionals)
        {
            throw new CliUsageException($"Expected {requiredPositionals} positional argument(s), received {positionals.Count}.");
        }
        return new ParsedArguments(positionals, options);
    }

    public string GetRequired(string name) => options.TryGetValue(name, out string? value)
        ? value
        : throw new CliUsageException($"Option '{name}' is required.");

    public ValidationSeverity GetSeverity(string name, ValidationSeverity fallback)
    {
        if (!options.TryGetValue(name, out string? value))
        {
            return fallback;
        }
        if (!Enum.TryParse(value, ignoreCase: true, out ValidationSeverity severity) ||
            !Enum.IsDefined(severity))
        {
            throw new CliUsageException($"Option '{name}' must be info, warning, or error.");
        }
        return severity;
    }

    public int GetInt32(string name, int fallback, int minimum, int maximum)
    {
        if (!options.TryGetValue(name, out string? value))
        {
            return fallback;
        }
        if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
        {
            throw new CliUsageException($"Option '{name}' must be from {minimum} to {maximum}.");
        }
        return parsed;
    }
}

internal sealed class CliUsageException(string message) : Exception(message);
