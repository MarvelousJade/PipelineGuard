using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using PipelineGuard.Api;
using PipelineGuard.Contracts;
using PipelineGuard.Core;
using Xunit;

namespace PipelineGuard.Api.Tests;

public sealed class SqliteReportStoreTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pipelineguard-tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_directory, "runs.db");
    private SqliteReportStore Store => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["PipelineGuard:DatabasePath"] = DatabasePath }).Build());
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Store.InitializeAsync(Token);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task List_OrdersInstantsNotLocalClockStrings()
    {
        ValidationReport newer = Report(Guid.NewGuid(), "2026-07-16T10:00:00+00:00");
        ValidationReport older = Report(Guid.NewGuid(), "2026-07-16T12:00:00+03:00");
        await Add(newer);
        await Add(older);

        IReadOnlyList<StoredRunSummary> runs = await Store.ListAsync(10, 0, Token);

        Assert.Equal(new[] { newer.RunId, older.RunId }, runs.Select(run => run.RunId));
    }

    [Fact]
    public async Task List_TiedInstantsUseRunIdForStablePages()
    {
        Guid first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await Add(Report(second, "2026-07-16T12:00:00+02:00"));
        await Add(Report(first, "2026-07-16T10:00:00+00:00"));

        Assert.Equal(first, Assert.Single(await Store.ListAsync(1, 0, Token)).RunId);
        Assert.Equal(second, Assert.Single(await Store.ListAsync(1, 1, Token)).RunId);
    }

    [Fact]
    public async Task DuplicateId_PreservesOriginalJsonAcrossReopen()
    {
        ValidationReport report = Report(Guid.NewGuid(), "2026-07-16T12:00:00+02:00");
        string original = JsonReportSerializer.Serialize(report);
        Assert.True(await Store.TryAddAsync(report, original, Token));
        Assert.False(await Store.TryAddAsync(report with { Project = "Replacement" }, "replacement", Token));
        await Store.InitializeAsync(Token);

        Assert.Equal(original, await Store.GetReportJsonAsync(report.RunId, Token));
        Assert.Single(await Store.ListAsync(10, 0, Token));
    }

    [Fact]
    public async Task Initialize_NormalizesLegacyOffsetsWithoutChangingReportJson()
    {
        ValidationReport older = Report(Guid.NewGuid(), "2026-07-16T12:00:00+03:00");
        ValidationReport newer = Report(Guid.NewGuid(), "2026-07-16T10:00:00+00:00");
        string original = JsonReportSerializer.Serialize(older);
        await Add(older);
        await Add(newer);
        // Simulate a database written by the original release.
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync(Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE validation_runs SET started_utc = $date, completed_utc = $date WHERE run_id = $id";
            command.Parameters.AddWithValue("$date", older.CompletedUtc.ToString("O"));
            command.Parameters.AddWithValue("$id", older.RunId.ToString("D"));
            await command.ExecuteNonQueryAsync(Token);
        }

        await Store.InitializeAsync(Token);
        await Store.InitializeAsync(Token); // Repeated startup is safe.

        Assert.Equal(newer.RunId, (await Store.ListAsync(10, 0, Token))[0].RunId);
        Assert.Equal(original, await Store.GetReportJsonAsync(older.RunId, Token));
    }

    [Fact]
    public async Task List_PreservesSubMillisecondChronology()
    {
        ValidationReport newer = Report(Guid.NewGuid(), "2026-07-16T10:00:00.0000002Z");
        ValidationReport older = Report(Guid.NewGuid(), "2026-07-16T10:00:00.0000001Z");
        await Add(older);
        await Add(newer);
        Assert.Equal(newer.RunId, (await Store.ListAsync(10, 0, Token))[0].RunId);
    }

    [Fact]
    public async Task MissingRun_ReturnsNull()
    {
        Assert.Null(await Store.GetReportJsonAsync(Guid.NewGuid(), Token));
        Assert.Empty(await Store.ListAsync(10, 0, Token));
    }

    private async Task Add(ValidationReport report) =>
        Assert.True(await Store.TryAddAsync(report, JsonReportSerializer.Serialize(report), Token));

    private static ValidationReport Report(Guid id, string completed) => new()
    {
        SchemaVersion = "1.0", RunId = id, ToolVersion = "test", Project = "Fixtures",
        EngineVersion = "5.6", Profile = "test", Scope = new[] { "/Game" },
        StartedUtc = DateTimeOffset.Parse(completed, System.Globalization.CultureInfo.InvariantCulture),
        CompletedUtc = DateTimeOffset.Parse(completed, System.Globalization.CultureInfo.InvariantCulture),
        Summary = new ValidationSummary
        {
            AssetsScanned = 0, RulesRun = 0, Errors = 0, Warnings = 0,
            Infos = 0, Suppressed = 0, DurationMs = 0
        },
        Findings = Array.Empty<ValidationFinding>()
    };
}
