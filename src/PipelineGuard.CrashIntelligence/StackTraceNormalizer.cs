using System.Text.RegularExpressions;

namespace PipelineGuard.CrashIntelligence;

public static partial class StackTraceNormalizer
{
    public const int FingerprintFrameLimit = 12;

    public static IReadOnlyList<string> Normalize(IEnumerable<string> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        return frames
            .Select(NormalizeFrame)
            .Where(frame => frame.Length > 0 && !IsCrashPlumbing(frame))
            .Take(FingerprintFrameLimit)
            .ToArray();
    }

    public static string NormalizeFrame(string? frame)
    {
        if (string.IsNullOrWhiteSpace(frame))
        {
            return string.Empty;
        }

        string normalized = CallstackPrefixRegex().Replace(frame.Trim(), string.Empty);
        normalized = LeadingAddressRegex().Replace(normalized, string.Empty);
        normalized = SourceLocationRegex().Replace(normalized, string.Empty);
        normalized = ModuleExtensionRegex().Replace(normalized, string.Empty);
        normalized = SymbolOffsetRegex().Replace(normalized, string.Empty);
        normalized = AddressInParenthesesRegex().Replace(normalized, string.Empty);

        // Parameter values and compiler-specific signatures vary between symbol servers.
        // The owning type and function name are the stable grouping signal.
        for (int pass = 0; pass < 3; pass++)
        {
            normalized = ArgumentsRegex().Replace(normalized, "()");
        }

        normalized = WhitespaceRegex().Replace(normalized, " ");
        return normalized.Trim(' ', '-', ':').ToLowerInvariant();
    }

    private static bool IsCrashPlumbing(string frame) =>
        frame.Contains("fdebug::assertfailed", StringComparison.Ordinal) ||
        frame.Contains("fplatformmisc::requestexit", StringComparison.Ordinal) ||
        frame.Contains("reportcrash", StringComparison.Ordinal) ||
        frame.Contains("crashreportclient", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*(?:\[callstack\]\s*)?(?:(?:#\d+|\d+\s*[:.)-])\s*)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CallstackPrefixRegex();

    [GeneratedRegex(@"^(?:0x)?[0-9a-f]{8,16}\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingAddressRegex();

    [GeneratedRegex(@"\s*\[(?:[a-z]:[\\/]|/).+?(?::line\s*|:)\d+\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceLocationRegex();

    [GeneratedRegex(@"\.(?:dll|exe|so|dylib)(?=!|\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModuleExtensionRegex();

    [GeneratedRegex(@"\s*\+\s*(?:0x)?[0-9a-f]+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SymbolOffsetRegex();

    [GeneratedRegex(@"\(\s*0x[0-9a-f]+\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressInParenthesesRegex();

    [GeneratedRegex(@"\([^()]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ArgumentsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
