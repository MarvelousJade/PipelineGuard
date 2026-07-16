using System.Diagnostics;
using PipelineGuard.Contracts;

namespace PipelineGuard.Core;

public sealed class UnrealValidationRunner
{
    public async Task<UnrealValidationResult> RunAsync(
        string editorPath,
        string projectPath,
        string reportPath,
        ValidationSeverity failureThreshold,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Timeout must be greater than zero and no more than two hours.");
        }
        if (!File.Exists(editorPath))
        {
            throw new FileNotFoundException("UnrealEditor-Cmd executable was not found.", editorPath);
        }
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Unreal project was not found.", projectPath);
        }

        ProcessStartInfo startInfo = UnrealProcessStartInfoBuilder.Create(
            editorPath,
            projectPath,
            reportPath);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Unreal failed to start.");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Unreal validation exceeded {timeout.TotalSeconds:0} seconds.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string stdout = await standardOutput.ConfigureAwait(false);
        string stderr = await standardError.ConfigureAwait(false);
        int rawExitCode = process.ExitCode;
        if (rawExitCode is not (0 or 1) || !File.Exists(reportPath))
        {
            return new UnrealValidationResult(rawExitCode, 3, null, stdout, stderr);
        }

        ValidationReport report = await JsonReportSerializer.ReadFileAsync(
            reportPath,
            cancellationToken).ConfigureAwait(false);
        int publicExitCode = ReportAnalyzer.MeetsFailureThreshold(report, failureThreshold) ? 1 : 0;
        return new UnrealValidationResult(rawExitCode, publicExitCode, report, stdout, stderr);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

public sealed record UnrealValidationResult(
    int RawExitCode,
    int ExitCode,
    ValidationReport? Report,
    string StandardOutput,
    string StandardError);
