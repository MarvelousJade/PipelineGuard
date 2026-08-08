using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.CrashIntelligence.Tests;

public sealed class CrashReportValidatorTests
{
    [Fact]
    public void Validate_AcceptsBoundedV1Report()
    {
        UnrealCrashReport report = CrashTestData.Report();

        CrashReportValidator.Validate(report);
    }

    [Fact]
    public void Validate_RejectsMissingIdentityAndStack()
    {
        UnrealCrashReport report = CrashTestData.Report(Guid.Empty) with
        {
            StackTrace = Array.Empty<string>()
        };

        CrashReportValidationException exception = Assert.Throws<CrashReportValidationException>(
            () => CrashReportValidator.Validate(report));

        Assert.Contains("crashId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("stackTrace", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CrashDocument_RejectsStackContainingOnlyCrashPlumbing()
    {
        CrashEnvelope envelope = CrashTestData.Envelope(frames: new[]
        {
            "UnrealEditor-Core.dll!FDebug::AssertFailed(char const*)",
            "CrashReportClient.exe!ReportCrash()"
        });

        Assert.Throws<CrashReportValidationException>(() =>
            CrashDocument.FromEnvelope(envelope, DateTimeOffset.UtcNow));
    }
}
