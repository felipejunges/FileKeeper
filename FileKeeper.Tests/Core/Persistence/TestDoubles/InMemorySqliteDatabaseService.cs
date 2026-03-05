using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using System.Data.SQLite;

namespace FileKeeper.Tests.Core.Persistence.TestDoubles;

internal sealed class InMemorySqliteDatabaseService : IDatabaseService
{
    private readonly SQLiteConnection _connection;

    public InMemorySqliteDatabaseService()
    {
        _connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
        _connection.Open();
    }

    public Task<ErrorOr<Success>> InitializeAsync(CancellationToken token)
        => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public Task<ErrorOr<int>> GetDatabaseVersionAsync(CancellationToken token)
        => Task.FromResult<ErrorOr<int>>(0);

    public Task<ErrorOr<long>> GetDatabaseSizeAsync(CancellationToken token)
        => Task.FromResult<ErrorOr<long>>(0L);

    public Task<ErrorOr<Success>> MigrateAsync(int targetVersion, CancellationToken token)
        => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public Task<ErrorOr<Success>> ExecuteAsync(string sql, CancellationToken token)
    {
        using var command = new SQLiteCommand(sql, _connection);
        command.ExecuteNonQuery();
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    public string GetDatabasePath() => ":memory:";

    public SQLiteConnection GetConnection() => _connection;

    public SQLiteTransaction BeginTransaction() => _connection.BeginTransaction();

    public void CommitTransaction()
    {
    }

    public void RollbackTransaction()
    {
    }

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}

