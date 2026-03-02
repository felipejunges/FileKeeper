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
    private SQLiteTransaction? _transaction;
    private int _currentVersion;
    private const int LatestDatabaseVersion = 1;

    // Initial DDL - executed only when database doesn't exist
    private static readonly string[] InitialSchema = new[]
    {
        @"
        CREATE TABLE Backups (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CreatedAt TEXT NOT NULL,
            CreatedFiles INTEGER NOT NULL,
            UpdatedFiles INTEGER NOT NULL,
            DeletedFiles INTEGER NOT NULL
        );",
        
        @"
        CREATE TABLE Files (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            BackupPath TEXT NOT NULL,
            RelativePath TEXT NOT NULL,
            FileName TEXT NOT NULL,
            IsDeleted INTEGER NOT NULL,
            DeletedAt INTEGER,
            FOREIGN KEY(DeletedAt) REFERENCES Backups(Id)
        );",
        
        @"
        CREATE TABLE FileVersions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FileId INTEGER NOT NULL,
            BackupId INTEGER NOT NULL,
            Size INTEGER NOT NULL,
            Hash TEXT NOT NULL,
            Content BLOB NOT NULL,
            FOREIGN KEY(FileId) REFERENCES Files(Id),
            FOREIGN KEY(BackupId) REFERENCES Backups(Id)
        );",
        
        @"CREATE INDEX idx_files_backup_relative_path ON Files(BackupPath, RelativePath);",
        @"CREATE INDEX idx_files_is_deleted ON Files(IsDeleted);",
        @"CREATE INDEX idx_files_deleted_at ON Files(DeletedAt);",
        @"CREATE INDEX idx_file_versions_file_id ON FileVersions(FileId);",
        @"CREATE INDEX idx_file_versions_backup_id ON FileVersions(BackupId);"
    };

    // Migrations - executed when database already exists and needs updates
    // ReSharper disable once CollectionNeverUpdated.Local
    private static readonly Dictionary<int, string[]> Migrations = new()
    {
        // Example migration for version 2
        // {
        //     2, new[]
        //     {
        //         @"ALTER TABLE Files ADD COLUMN CurrentHash TEXT;",
        //         @"UPDATE Files SET CurrentHash = (SELECT Hash FROM FileVersions WHERE FileVersions.FileId = Files.Id ORDER BY BackupId DESC LIMIT 1);",
        //         @"CREATE INDEX idx_files_current_hash ON Files(CurrentHash);"
        //     }
        // }
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
            
            bool databaseExists = File.Exists(_databasePath);
            
            await OpenConnectionAsync(token);

            if (!databaseExists)
            {
                // Create new database with initial schema
                var initResult = await InitializeNewDatabaseAsync(token);
                if (initResult.IsError)
                    return initResult;
            }
            else
            {
                // Existing database - run migrations if needed
                var versionResult = await GetDatabaseVersionAsync(token);
                _currentVersion = versionResult.IsError ? 0 : versionResult.Value;

                if (_currentVersion < LatestDatabaseVersion)
                {
                    var migrateResult = await MigrateAsync(LatestDatabaseVersion, token);
                    if (migrateResult.IsError)
                        return migrateResult;
                }
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure("database.initialization_failed", $"Failed to initialize database: {ex.Message}");
        }
    }

    private async Task<ErrorOr<Success>> InitializeNewDatabaseAsync(CancellationToken token)
    {
        try
        {
            using var transaction = _connection!.BeginTransaction();
            try
            {
                foreach (var sql in InitialSchema)
                {
                    using var cmd = new SQLiteCommand(sql, _connection);
                    await Task.Run(() => cmd.ExecuteNonQuery(), token);
                }

                // Set initial version to 1
                using var updateVersionCmd = new SQLiteCommand("PRAGMA user_version = 1;", _connection);
                await Task.Run(() => updateVersionCmd.ExecuteNonQuery(), token);

                transaction.Commit();
                _currentVersion = 1;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Error.Failure("database.initialization_failed", $"Failed to initialize new database: {ex.Message}");
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure("database.initialization_failed", $"Failed to initialize new database: {ex.Message}");
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
            return result is int version ? version
                : result is long versionLong ? (int)versionLong
                : 0;
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

    public SQLiteConnection GetConnection()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
            _connection.Open();
        }

        return _connection;
    }
    
    public SQLiteTransaction BeginTransaction()
    {
        var connection = GetConnection();
        
        _transaction = connection.BeginTransaction();
        return _transaction;
    }
    
    public void CommitTransaction()
    {
        _transaction?.Commit();
        _transaction?.Dispose();
    }
    
    public void RollbackTransaction()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
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

