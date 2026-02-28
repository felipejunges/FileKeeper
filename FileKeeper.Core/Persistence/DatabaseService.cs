using System.Data.SQLite;
using ErrorOr;
using FileKeeper.Core.Application;
using FileKeeper.Core.Interfaces.Persistence;

namespace FileKeeper.Core.Persistence;

/// <summary>
/// Manages SQLite database operations including initialization and migrations.
/// </summary>
public class DatabaseService : IDatabaseService, IAsyncDisposable
{
    private readonly string _databasePath;
    private SQLiteConnection? _connection;
    private int _currentVersion;
    private const int LatestDatabaseVersion = 1;

    private static readonly Dictionary<int, string[]> Migrations = new()
    {
        {
            1, new[]
            {
                @"
                CREATE TABLE IF NOT EXISTS Files (
                    Id TEXT PRIMARY KEY,
                    Path TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL,
                    DeletedVersionNumber INTEGER
                );",
                @"
                CREATE TABLE IF NOT EXISTS FileVersions (
                    Id TEXT PRIMARY KEY,
                    FileId TEXT NOT NULL,
                    Content BLOB NOT NULL,
                    Hash TEXT NOT NULL,
                    Size INTEGER NOT NULL,
                    VersionNumber INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY(FileId) REFERENCES Files(Id)
                );",
                
                @"CREATE INDEX IF NOT EXISTS idx_files_path ON Files(Path);",
                @"CREATE INDEX IF NOT EXISTS idx_files_is_deleted ON Files(IsDeleted);",
                @"CREATE INDEX IF NOT EXISTS idx_file_versions_file_id ON FileVersions(FileId);",
                @"CREATE INDEX IF NOT EXISTS idx_file_versions_hash ON FileVersions(Hash);",
                @"CREATE INDEX IF NOT EXISTS idx_file_versions_created_at ON FileVersions(CreatedAt);"
            }
        }
    };

    public DatabaseService()
    {
        var paths = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileKeeper"
        };

        if (ApplicationInfo.IsDebug)
            paths.Add("debug");

        paths.Add("filekeeper.db");
        _databasePath = Path.Combine(paths.ToArray());
    }

    public string GetDatabasePath() => _databasePath;

    public async Task<ErrorOr<Success>> InitializeAsync(CancellationToken token)
    {
        try
        {
            EnsureDirectoryExists();
            await OpenConnectionAsync(token);

            var versionResult = await GetDatabaseVersionAsync(token);
            _currentVersion = versionResult.IsError ? 0 : versionResult.Value;

            if (_currentVersion < LatestDatabaseVersion)
            {
                var migrateResult = await MigrateAsync(LatestDatabaseVersion, token);
                if (migrateResult.IsError)
                    return migrateResult;
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure("database.initialization_failed", $"Failed to initialize database: {ex.Message}");
        }
    }

    public async Task<ErrorOr<int>> GetDatabaseVersionAsync(CancellationToken token)
    {
        try
        {
            await OpenConnectionAsync(token);

            const string sql = "PRAGMA user_version;";
            using var cmd = new SQLiteCommand(sql, _connection);
            
            var result = await Task.Run(() => cmd.ExecuteScalar(), token);
            return result is int version ? version : 0;
        }
        catch (Exception ex)
        {
            return Error.Failure("database.version_check_failed", $"Failed to get database version: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> MigrateAsync(int targetVersion, CancellationToken token)
    {
        try
        {
            await OpenConnectionAsync(token);

            // Execute migrations from current version up to target version
            for (int version = _currentVersion + 1; version <= targetVersion; version++)
            {
                if (!Migrations.TryGetValue(version, out var migrationSqls))
                {
                    return Error.Failure("database.migration_not_found", $"Migration for version {version} not found");
                }

                using var transaction = _connection!.BeginTransaction();
                try
                {
                    foreach (var sql in migrationSqls)
                    {
                        using var cmd = new SQLiteCommand(sql, _connection);
                        await Task.Run(() => cmd.ExecuteNonQuery(), token);
                    }

                    // Update user_version
                    using var updateVersionCmd = new SQLiteCommand($"PRAGMA user_version = {version};", _connection);
                    await Task.Run(() => updateVersionCmd.ExecuteNonQuery(), token);

                    transaction.Commit();
                    _currentVersion = version;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Error.Failure("database.migration_failed", $"Migration to version {version} failed: {ex.Message}");
                }
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure("database.migration_failed", $"Failed to migrate database: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> ExecuteAsync(string sql, CancellationToken token)
    {
        try
        {
            await OpenConnectionAsync(token);

            using var cmd = new SQLiteCommand(sql, _connection);
            await Task.Run(() => cmd.ExecuteNonQuery(), token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure("database.execution_failed", $"Failed to execute SQL: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            if (_connection.State == System.Data.ConnectionState.Open)
            {
                await Task.Run(() => _connection.Close());
            }

            _connection.Dispose();
            _connection = null;
        }
    }

    public System.Data.SQLite.SQLiteConnection GetConnection()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
            _connection.Open();
        }

        return _connection;
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task OpenConnectionAsync(CancellationToken token)
    {
        if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
            return;

        _connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
        await Task.Run(() => _connection.Open(), token);
    }
}

