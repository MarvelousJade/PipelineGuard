using System.Diagnostics;

namespace PipelineGuard.Core;

public static class UnrealProcessStartInfoBuilder
{
    public static ProcessStartInfo Create(
        string editorPath,
        string projectPath,
        string reportPath)
    {
        string editor = RequireFullyQualifiedPath(editorPath, nameof(editorPath));
        string project = RequireFullyQualifiedPath(projectPath, nameof(projectPath));
        string report = RequireFullyQualifiedPath(reportPath, nameof(reportPath));

        var startInfo = new ProcessStartInfo
        {
            FileName = editor,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("-run=PipelineGuard");
        startInfo.ArgumentList.Add($"-Report={report}");
        startInfo.ArgumentList.Add("-unattended");
        startInfo.ArgumentList.Add("-nop4");
        startInfo.ArgumentList.Add("-nullrhi");
        return startInfo;
    }

    private static string RequireFullyQualifiedPath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Path must be fully qualified.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}
