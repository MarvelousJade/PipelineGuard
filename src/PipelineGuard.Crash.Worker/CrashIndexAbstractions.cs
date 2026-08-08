using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.Crash.Worker;

public enum CrashIndexWriteOutcome
{
    Created,
    Duplicate
}

public interface ICrashIndex
{
    Task<CrashIndexWriteOutcome> TryCreateAsync(
        CrashDocument document,
        CancellationToken cancellationToken);
}

public sealed class CrashIndexTransientException : Exception
{
    public CrashIndexTransientException(string message)
        : base(message)
    {
    }

    public CrashIndexTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class CrashIndexPermanentException : Exception
{
    public CrashIndexPermanentException(string message)
        : base(message)
    {
    }
}
