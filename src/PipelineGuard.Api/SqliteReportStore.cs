using Microsoft.Data.Sqlite;
using PipelineGuard.Contracts;

namespace PipelineGuard.Api;

public sealed record StoredRunSummary(
    Guid RunId,
    DateTimeOffset CompletedUtc,
    string Project,
    string Profile,
    string EngineVersion,
    int AssetsScanned,
    int RulesRun,
    int Errors,
    int Warnings,
    int Infos,
    int Suppressed,
    long DurationMs);

public sealed class SqliteReportStore
{
    private readonly string _connectionString;

    public SqliteReportStore(IConfiguration configuration)
    {
        string configuredPath = configuration["PipelineGuard:DatabasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, ".pipelineguard", "pipelineguard.db");
        string fullPath = Path.GetFullPath(configuredPath);
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS validation_runs (
                run_id TEXT PRIMARY KEY,
                started_utc TEXT NOT NULL,
                completed_utc TEXT NOT NULL,
                project TEXT NOT NULL,
                profile TEXT NOT NULL,
                engine_version TEXT NOT NULL,
                assets_scanned INTEGER NOT NULL,
                rules_run INTEGER NOT NULL,
                errors INTEGER NOT NULL,
                warnings INTEGER NOT NULL,
                infos INTEGER NOT NULL,
                suppressed INTEGER NOT NULL,
                duration_ms INTEGER NOT NULL,
                report_json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_validation_runs_completed_utc
                ON validation_runs(completed_utc DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await NormalizeLegacyTimestampsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    // Keep sortable metadata canonical without rewriting the original uploaded JSON.
    private static async Task NormalizeLegacyTimestampsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        var legacyRows = new List<(string Id, string Started, string Completed)>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                SELECT run_id, started_utc, completed_utc FROM validation_runs
                WHERE substr(started_utc, -6) <> '+00:00'
                   OR substr(completed_utc, -6) <> '+00:00';
                """;
            await using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                legacyRows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        foreach (var row in legacyRows)
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE validation_runs SET started_utc = $started, completed_utc = $completed
                WHERE run_id = $id;
                """;
            update.Parameters.AddWithValue("$id", row.Id);
            update.Parameters.AddWithValue("$started", FormatUtc(DateTimeOffset.Parse(
                row.Started, System.Globalization.CultureInfo.InvariantCulture)));
            update.Parameters.AddWithValue("$completed", FormatUtc(DateTimeOffset.Parse(
                row.Completed, System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        transaction.Commit();
    }

    private static string FormatUtc(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    public async Task<bool> TryAddAsync(
        ValidationReport report,
        string reportJson,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO validation_runs (
                run_id, started_utc, completed_utc, project, profile, engine_version,
                assets_scanned, rules_run, errors, warnings, infos, suppressed,
                duration_ms, report_json)
            VALUES (
                $run_id, $started_utc, $completed_utc, $project, $profile, $engine_version,
                $assets_scanned, $rules_run, $errors, $warnings, $infos, $suppressed,
                $duration_ms, $report_json);
            """;
        command.Parameters.AddWithValue("$run_id", report.RunId.ToString("D"));
        command.Parameters.AddWithValue("$started_utc", FormatUtc(report.StartedUtc));
        command.Parameters.AddWithValue("$completed_utc", FormatUtc(report.CompletedUtc));
        command.Parameters.AddWithValue("$project", report.Project);
        command.Parameters.AddWithValue("$profile", report.Profile);
        command.Parameters.AddWithValue("$engine_version", report.EngineVersion);
        command.Parameters.AddWithValue("$assets_scanned", report.Summary.AssetsScanned);
        command.Parameters.AddWithValue("$rules_run", report.Summary.RulesRun);
        command.Parameters.AddWithValue("$errors", report.Summary.Errors);
        command.Parameters.AddWithValue("$warnings", report.Summary.Warnings);
        command.Parameters.AddWithValue("$infos", report.Summary.Infos);
        command.Parameters.AddWithValue("$suppressed", report.Summary.Suppressed);
        command.Parameters.AddWithValue("$duration_ms", report.Summary.DurationMs);
        command.Parameters.AddWithValue("$report_json", reportJson);

        int changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return changed == 1;
    }

    public async Task<string?> GetReportJsonAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT report_json FROM validation_runs WHERE run_id = $run_id;";
        command.Parameters.AddWithValue("$run_id", runId.ToString("D"));
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    public async Task<IReadOnlyList<StoredRunSummary>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var runs = new List<StoredRunSummary>(limit);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id, completed_utc, project, profile, engine_version,
                   assets_scanned, rules_run, errors, warnings, infos, suppressed, duration_ms
              FROM validation_runs
             ORDER BY completed_utc DESC, run_id ASC
             LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            runs.Add(new StoredRunSummary(
                Guid.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt64(11)));
        }

        return runs;
    }
}
