namespace PipelineGuard.CrashIntelligence;

public sealed class CrashReportValidationException : Exception
{
    public CrashReportValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
