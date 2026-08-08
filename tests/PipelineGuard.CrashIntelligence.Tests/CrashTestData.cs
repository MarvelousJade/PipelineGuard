using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.CrashIntelligence.Tests;

internal static class CrashTestData
{
    public static UnrealCrashReport Report(Guid? crashId = null, IReadOnlyList<string>? frames = null) => new()
    {
        SchemaVersion = "1.0",
        CrashId = crashId ?? Guid.NewGuid(),
        OccurredAtUtc = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
        Project = "PipelineGuardHost",
        Build = new CrashBuild
        {
            Version = "1.4.27",
            Branch = "main",
            Changelist = 10427
        },
        Platform = "Linux",
        ErrorType = "AccessViolation",
        ErrorMessage = "Unhandled exception: EXCEPTION_ACCESS_VIOLATION reading address 0x18",
        StackTrace = frames ?? new[]
        {
            "0x00007ff6 UnrealEditor-Game.dll!UInventoryComponent::RemoveItem(FGuid const&) [D:\\build\\Game\\Inventory.cpp:line 418]",
            "0x00007ff7 UnrealEditor-Game.dll!APlayerController::Tick(float) [D:\\build\\Game\\Player.cpp:line 91]"
        }
    };

    public static CrashEnvelope Envelope(Guid? crashId = null, IReadOnlyList<string>? frames = null)
    {
        UnrealCrashReport report = Report(crashId, frames);
        return CrashEnvelope.Create(
            report,
            new DateTimeOffset(2026, 7, 16, 12, 0, 1, TimeSpan.Zero));
    }
}
