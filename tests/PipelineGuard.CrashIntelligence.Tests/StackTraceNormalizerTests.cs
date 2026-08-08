using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.CrashIntelligence.Tests;

public sealed class StackTraceNormalizerTests
{
    [Fact]
    public void NormalizeFrame_RemovesAddressSourceAndSymbolNoise()
    {
        const string frame = "[Callstack] 0x00007FF61234ABCD UnrealEditor-Game.dll!UInventoryComponent::RemoveItem(FGuid const&) [D:\\agent\\Game\\Inventory.cpp:line 418]";

        string normalized = StackTraceNormalizer.NormalizeFrame(frame);

        Assert.Equal("unrealeditor-game!uinventorycomponent::removeitem()", normalized);
    }

    [Fact]
    public void EquivalentWindowsAndLinuxStacks_HaveSameFingerprint()
    {
        string[] windows =
        {
            "[Callstack] 0x00007FF61234ABCD UnrealEditor-Core.dll!FDebug::AssertFailed(char const*) [D:\\Engine\\Debug.cpp:line 100]",
            "0x00007FF612340001 UnrealEditor-Game.dll!UInventoryComponent::RemoveItem(FGuid const&) [D:\\Game\\Inventory.cpp:line 418]",
            "0x00007FF612340002 UnrealEditor-Game.dll!APlayerController::Tick(float) [D:\\Game\\Player.cpp:line 91]"
        };
        string[] linux =
        {
            "#0 00007f9a12340000 UnrealEditor-Core.so!FDebug::AssertFailed(char*) [/engine/Debug.cpp:101]",
            "#1 00007f9a12340001 UnrealEditor-Game.so!UInventoryComponent::RemoveItem(FGuid) + 0x9a [/workspace/Inventory.cpp:500]",
            "#2 00007f9a12340002 UnrealEditor-Game.so!APlayerController::Tick(double) + 2f [/workspace/Player.cpp:92]"
        };

        IReadOnlyList<string> first = StackTraceNormalizer.Normalize(windows);
        IReadOnlyList<string> second = StackTraceNormalizer.Normalize(linux);

        Assert.Equal(first, second);
        Assert.Equal(
            CrashFingerprint.Compute("AccessViolation", first),
            CrashFingerprint.Compute("accessviolation", second));
    }

    [Fact]
    public void OneHundredAddressAndLineVariants_FormOneGroup()
    {
        string[] fingerprints = Enumerable.Range(1, 100)
            .Select(index => new[]
            {
                $"0x{index:X16} UnrealEditor-Game.dll!UInventoryComponent::RemoveItem(int) [D:\\run-{index}\\Inventory.cpp:line {100 + index}]",
                $"0x{index + 1:X16} UnrealEditor-Game.dll!APlayerController::Tick(float) [D:\\run-{index}\\Player.cpp:line {200 + index}]"
            })
            .Select(StackTraceNormalizer.Normalize)
            .Select(frames => CrashFingerprint.Compute("AccessViolation", frames))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Single(fingerprints);
    }

    [Fact]
    public void DifferentTopApplicationFrame_HasDifferentFingerprint()
    {
        IReadOnlyList<string> first = StackTraceNormalizer.Normalize(CrashTestData.Report().StackTrace);
        IReadOnlyList<string> second = StackTraceNormalizer.Normalize(new[]
        {
            "UnrealEditor-Game.dll!UQuestComponent::AdvanceQuest(int)",
            "UnrealEditor-Game.dll!APlayerController::Tick(float)"
        });

        Assert.NotEqual(
            CrashFingerprint.Compute("AccessViolation", first),
            CrashFingerprint.Compute("AccessViolation", second));
    }
}
