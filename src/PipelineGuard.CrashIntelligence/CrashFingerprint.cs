using System.Security.Cryptography;
using System.Text;

namespace PipelineGuard.CrashIntelligence;

public static class CrashFingerprint
{
    public static string Compute(string errorType, IReadOnlyList<string> normalizedFrames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorType);
        ArgumentNullException.ThrowIfNull(normalizedFrames);

        string canonical = string.Join(
            '\n',
            new[] { errorType.Trim().ToLowerInvariant() }.Concat(normalizedFrames));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
