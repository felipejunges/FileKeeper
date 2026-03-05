using ErrorOr;
using System.Data.SQLite;

namespace FileKeeper.Core.Interfaces.Persistence;

public interface IDatabaseService
{
    Task<ErrorOr<Success>> InitializeAsync(CancellationToken token);

    Task<ErrorOr<int>> GetDatabaseVersionAsync(CancellationToken token);

    Task<ErrorOr<long>> GetDatabaseSizeAsync(CancellationToken token);

    Task<ErrorOr<Success>> MigrateAsync(int targetVersion, CancellationToken token);

    Task<ErrorOr<Success>> ExecuteAsync(string sql, CancellationToken token);

    string GetDatabasePath();

    SQLiteConnection GetConnection();

    SQLiteTransaction BeginTransaction();

    void CommitTransaction();

    void RollbackTransaction();

    ValueTask DisposeAsync();
}

