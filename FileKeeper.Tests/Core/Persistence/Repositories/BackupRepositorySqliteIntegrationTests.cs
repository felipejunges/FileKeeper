using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Tests.Core.Persistence.TestDoubles;
using System.Data.SQLite;

namespace FileKeeper.Tests.Core.Persistence.Repositories;

public class BackupRepositorySqliteIntegrationTests
{
    [Fact(DisplayName = "01 - InsertAsync should persist backup and update generated Id")]
    public async Task InsertAsync_ShouldPersistBackupAndUpdateGeneratedId()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);
        var backup = new Backup(0, new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc), 2, 1, 0, 0);

        var insertResult = await sut.InsertAsync(backup, CancellationToken.None);

        Assert.False(insertResult.IsError);
        Assert.True(insertResult.Value > 0);
        Assert.Equal(insertResult.Value, backup.Id);

        var fetched = await sut.GetByIdAsync(insertResult.Value, CancellationToken.None);

        Assert.False(fetched.IsError);
        Assert.Equal(backup.Id, fetched.Value.Id);
        Assert.Equal(backup.CreatedFiles, fetched.Value.CreatedFiles);
        Assert.Equal(backup.UpdatedFiles, fetched.Value.UpdatedFiles);
        Assert.Equal(backup.DeletedFiles, fetched.Value.DeletedFiles);
    }

    [Fact(DisplayName = "02 - GetByIdAsync should return NotFound when backup does not exist")]
    public async Task GetByIdAsync_ShouldReturnNotFound_WhenBackupDoesNotExist()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);

        var result = await sut.GetByIdAsync(999, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact(DisplayName = "03 - GetAllAsync should return backups ordered by CreatedAt descending")]
    public async Task GetAllAsync_ShouldReturnBackupsOrderedByCreatedAtDescending()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);

        var older = new Backup(0, new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc), 1, 0, 0, 0);
        var newer = new Backup(0, new DateTime(2026, 3, 5, 11, 0, 0, DateTimeKind.Utc), 3, 1, 1, 0);

        await sut.InsertAsync(older, CancellationToken.None);
        await sut.InsertAsync(newer, CancellationToken.None);

        var allResult = await sut.GetAllAsync(CancellationToken.None);

        Assert.False(allResult.IsError);
        var backups = allResult.Value.ToList();
        Assert.Equal(2, backups.Count);
        Assert.Equal(newer.Id, backups[0].Id);
        Assert.Equal(older.Id, backups[1].Id);
    }

    [Fact(DisplayName = "04 - GetNextBackupAfterAsync should return first backup after informed CreatedAt")]
    public async Task GetNextBackupAfterAsync_ShouldReturnFirstBackupAfterCreatedAt()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);

        var first = new Backup(0, new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc), 1, 0, 0, 0);
        var second = new Backup(0, new DateTime(2026, 3, 5, 11, 0, 0, DateTimeKind.Utc), 2, 1, 0, 0);
        var third = new Backup(0, new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc), 3, 1, 1, 0);

        await sut.InsertAsync(first, CancellationToken.None);
        await sut.InsertAsync(second, CancellationToken.None);
        await sut.InsertAsync(third, CancellationToken.None);

        var result = await sut.GetNextBackupAfterAsync(first.CreatedAt, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(second.Id, result.Value.Id);
    }

    [Fact(DisplayName = "05 - GetNextBackupAfterAsync should return NotFound when there is no newer backup")]
    public async Task GetNextBackupAfterAsync_ShouldReturnNotFound_WhenNoNewerBackupExists()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);
        var backup = new Backup(0, new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc), 1, 0, 0, 0);
        await sut.InsertAsync(backup, CancellationToken.None);

        var result = await sut.GetNextBackupAfterAsync(backup.CreatedAt, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact(DisplayName = "06 - GetOldestAsync should return oldest backup by CreatedAt")]
    public async Task GetOldestAsync_ShouldReturnOldestBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);

        var older = new Backup(0, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), 1, 0, 0, 0);
        var newer = new Backup(0, new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc), 2, 1, 0, 0);

        await sut.InsertAsync(newer, CancellationToken.None);
        await sut.InsertAsync(older, CancellationToken.None);

        var result = await sut.GetOldestAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(older.Id, result.Value.Id);
    }

    [Fact(DisplayName = "07 - GetOldestAsync should return NotFound when table is empty")]
    public async Task GetOldestAsync_ShouldReturnNotFound_WhenEmpty()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateBackupsTableAsync(databaseService.GetConnection());

        var sut = new BackupRepository(databaseService);

        var result = await sut.GetOldestAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    private static async Task CreateBackupsTableAsync(SQLiteConnection connection)
    {
        const string sql = @"
            CREATE TABLE Backups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                CreatedFiles INTEGER NOT NULL,
                UpdatedFiles INTEGER NOT NULL,
                DeletedFiles INTEGER NOT NULL,
                TotalSize INTEGER NOT NULL
            );";

        using var command = new SQLiteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
