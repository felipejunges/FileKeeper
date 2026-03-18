using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using System.Data.SQLite;

namespace FileKeeper.Tests.Integrations.TestDoubles;

internal sealed class InMemorySqliteDatabaseService : IDatabaseService
{
    private readonly SQLiteConnection _connection;

    public InMemorySqliteDatabaseService()
    {
        _connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
        _connection.Open();
        
        // Enable foreign key constraints (this do not look to be needed)
        //using var pragmaCommand = new SQLiteCommand("PRAGMA foreign_keys = ON;", _connection);
        //pragmaCommand.ExecuteNonQuery();
    }

    public async Task<ErrorOr<Success>> InitializeAsync(CancellationToken token)
    {
        foreach (var sql in InitialSchema)
        {
            await using var cmd = new SQLiteCommand(sql, _connection);
            await cmd.ExecuteNonQueryAsync(token);
        }
        
        return Result.Success;
    }

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
    
    // TODO: duplicated code
    private static readonly string[] InitialSchema = new[]
    {
        @"
        CREATE TABLE Backups (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CreatedAt TEXT NOT NULL,
            CreatedFiles INTEGER NOT NULL,
            UpdatedFiles INTEGER NOT NULL,
            DeletedFiles INTEGER NOT NULL,
            TotalSize INTEGER NOT NULL
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
            IsNew INTEGER NOT NULL,
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
}

