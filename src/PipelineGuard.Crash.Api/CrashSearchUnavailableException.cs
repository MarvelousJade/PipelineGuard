namespace PipelineGuard.Crash.Api;

public sealed class CrashSearchUnavailableException : Exception
{
    public CrashSearchUnavailableException(string message)
        : base(message)
    {
    }

    public CrashSearchUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
