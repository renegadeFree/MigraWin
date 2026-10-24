using Microsoft.Data.Sqlite;

namespace MigraWin.Services;

public sealed record ActivityRecord(
    long Id,
    DateTimeOffset Time,
    string Title,
    string Detail,
    string Kind);

public sealed class HistoryStore
{
    private readonly string _connectionString;

    public HistoryStore(string? path = null)
    {
        var databasePath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "history.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA user_version=1;
            CREATE TABLE IF NOT EXISTS activities (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                time_utc TEXT NOT NULL,
                title TEXT NOT NULL,
                detail TEXT NOT NULL,
                kind TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_activities_time ON activities(time_utc DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> RecordAsync(
        DateTimeOffset time,
        string title,
        string detail,
        string kind,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO activities(time_utc, title, detail, kind)
            VALUES ($time, $title, $detail, $kind);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$time", time.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$detail", detail);
        command.Parameters.AddWithValue("$kind", kind);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<ActivityRecord>> LoadRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ActivityRecord>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, time_utc, title, detail, kind
            FROM activities
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ActivityRecord(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return result;
    }

    public async Task PruneAsync(int keep = 500, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM activities
            WHERE id NOT IN (
                SELECT id FROM activities ORDER BY id DESC LIMIT $keep
            );
            """;
        command.Parameters.AddWithValue("$keep", Math.Max(100, keep));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
