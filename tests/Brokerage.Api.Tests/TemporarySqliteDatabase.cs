using Microsoft.Data.Sqlite;

namespace Brokerage.Api.Tests;

/// <summary>
/// Gives API integration tests a fresh SQLite database and removes it after the host is disposed.
/// </summary>
internal sealed class TemporarySqliteDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"brokerage-api-tests-{Guid.NewGuid():N}.db");

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = _path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    public Dictionary<string, string?> Configuration => new()
    {
        ["ConnectionStrings:BrokerageDb"] = ConnectionString,
        ["DatabaseInitialization:Mode"] = "Migrate"
    };

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = _path + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }

        return ValueTask.CompletedTask;
    }
}
