using ErrorOr;

namespace FileKeeper.Core.Interfaces.Persistence;

public interface IDatabaseService
{
    /// <summary>
    /// Initializes the database, creating tables if necessary and applying migrations.
    /// </summary>
    Task<ErrorOr<Success>> InitializeAsync(CancellationToken token);

    /// <summary>
    /// Gets the current database version.
    /// </summary>
    Task<ErrorOr<int>> GetDatabaseVersionAsync(CancellationToken token);

    /// <summary>
    /// Migrates the database to a specific version.
    /// </summary>
    Task<ErrorOr<Success>> MigrateAsync(int targetVersion, CancellationToken token);

    /// <summary>
    /// Executes a raw SQL query that returns no results.
    /// </summary>
    Task<ErrorOr<Success>> ExecuteAsync(string sql, CancellationToken token);

    /// <summary>
    /// Gets the database file path.
    /// </summary>
    string GetDatabasePath();

    /// <summary>
    /// Gets the reusable database connection.
    /// </summary>
    System.Data.SQLite.SQLiteConnection GetConnection();

    /// <summary>
    /// Disposes the database connection.
    /// </summary>
    ValueTask DisposeAsync();
}

