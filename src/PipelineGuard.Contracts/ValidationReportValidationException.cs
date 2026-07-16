namespace PipelineGuard.Contracts;

public sealed class ValidationReportValidationException : Exception
{
    public ValidationReportValidationException(IReadOnlyList<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
