using System.Diagnostics;

namespace PipelineGuard.Core.Tests;

public sealed class UnrealProcessStartInfoBuilderTests
{
    [Fact]
    public void Create_UsesArgumentListWithoutShellExecution()
    {
        (string editor, string project, string report) = FullyQualifiedPaths();

        ProcessStartInfo startInfo = UnrealProcessStartInfoBuilder.Create(editor, project, report);

        Assert.Equal(editor, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Empty(startInfo.Arguments);
        Assert.Equal(
            [project, "-run=PipelineGuard", $"-Report={report}", "-unattended", "-nop4", "-nullrhi"],
            startInfo.ArgumentList);
    }

    [Fact]
    public void Create_PreservesSpacesAndShellMetacharactersInsideSingleArguments()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Pipeline Guard & tools"));
        string editor = Path.Combine(root, "UE Editor", "UnrealEditor-Cmd.exe");
        string project = Path.Combine(root, "Game;Write-Host injected", "Game.uproject");
        string report = Path.Combine(root, "reports", "result & more.json");

        ProcessStartInfo startInfo = UnrealProcessStartInfoBuilder.Create(editor, project, report);

        Assert.Equal(project, startInfo.ArgumentList[0]);
        Assert.Equal($"-Report={report}", startInfo.ArgumentList[2]);
        Assert.Equal(6, startInfo.ArgumentList.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Create_RejectsRelativePaths(int relativeParameter)
    {
        (string editor, string project, string report) = FullyQualifiedPaths();
        string relative = Path.Combine("relative", "value");
        string actualEditor = relativeParameter == 0 ? relative : editor;
        string actualProject = relativeParameter == 1 ? relative : project;
        string actualReport = relativeParameter == 2 ? relative : report;

        Assert.Throws<ArgumentException>(() => UnrealProcessStartInfoBuilder.Create(
            actualEditor,
            actualProject,
            actualReport));
    }

    [Fact]
    public void Create_RejectsWhitespacePath()
    {
        (_, string project, string report) = FullyQualifiedPaths();

        Assert.Throws<ArgumentException>(() => UnrealProcessStartInfoBuilder.Create(
            " ",
            project,
            report));
    }

    [Fact]
    public void Create_CanonicalizesFullyQualifiedPaths()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PipelineGuard"));
        string editorWithParentSegment = Path.Combine(
            root,
            "intermediate",
            "..",
            "UnrealEditor-Cmd.exe");
        string project = Path.Combine(root, "Game.uproject");
        string report = Path.Combine(root, "report.json");

        ProcessStartInfo startInfo = UnrealProcessStartInfoBuilder.Create(
            editorWithParentSegment,
            project,
            report);

        Assert.Equal(Path.GetFullPath(editorWithParentSegment), startInfo.FileName);
    }

    private static (string Editor, string Project, string Report) FullyQualifiedPaths()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PipelineGuard Tests"));
        return (
            Path.Combine(root, "UnrealEditor-Cmd.exe"),
            Path.Combine(root, "BlackWood.uproject"),
            Path.Combine(root, "report.json"));
    }
}
