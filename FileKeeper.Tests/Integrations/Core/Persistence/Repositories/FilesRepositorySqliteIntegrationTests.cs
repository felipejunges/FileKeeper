using FileKeeper.Core.Models.Entities;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Tests.Integrations.Core.Persistence.TestDoubles;
using System.Data.SQLite;

namespace FileKeeper.Tests.Integrations.Core.Persistence.Repositories;

public class FilesRepositorySqliteIntegrationTests
{
    [Fact(DisplayName = "01 - InsertAsync should persist file and update generated Id")]
    public async Task InsertAsync_ShouldPersistFileAndUpdateGeneratedId()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);
        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");

        var insertResult = await sut.InsertAsync(file, CancellationToken.None);

        Assert.False(insertResult.IsError);
        Assert.True(insertResult.Value > 0);
        Assert.Equal(insertResult.Value, file.Id);
    }

    [Fact(DisplayName = "02 - InsertVersionAsync should persist file version and update generated Id")]
    public async Task InsertVersionAsync_ShouldPersistFileVersionAndUpdateGeneratedId()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);
        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        
        var fileInsertResult = await sut.InsertAsync(file, CancellationToken.None);
        Assert.False(fileInsertResult.IsError);

        var fileVersion = FileVersion.CreateNew(
            fileId: fileInsertResult.Value,
            backupId: 1,
            isNew: true,
            size: 1024,
            hash: "abc123",
            content: new byte[] { 1, 2, 3, 4, 5 });

        var versionInsertResult = await sut.InsertVersionAsync(fileVersion, CancellationToken.None);

        Assert.False(versionInsertResult.IsError);
        Assert.True(versionInsertResult.Value > 0);
        Assert.Equal(versionInsertResult.Value, fileVersion.Id);
    }

    [Fact(DisplayName = "03 - GetFilesInBackupAsync should return files with their latest version in backup")]
    public async Task GetFilesInBackupAsync_ShouldReturnFilesWithLatestVersionInBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        // Insert files
        var file1 = FileModel.CreateNew("/backup/path", "folder1", "file1.txt");
        var file2 = FileModel.CreateNew("/backup/path", "folder2", "file2.txt");

        var file1Id = (await sut.InsertAsync(file1, CancellationToken.None)).Value;
        var file2Id = (await sut.InsertAsync(file2, CancellationToken.None)).Value;

        // Insert versions for backup 1
        var version1 = FileVersion.CreateNew(file1Id, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var version2 = FileVersion.CreateNew(file2Id, 1, true, 2048, "hash2", new byte[] { 3, 4 });

        await sut.InsertVersionAsync(version1, CancellationToken.None);
        await sut.InsertVersionAsync(version2, CancellationToken.None);

        // Act
        var result = await sut.GetFilesInBackupAsync(1, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var filesInBackup = result.Value.ToList();
        Assert.Equal(2, filesInBackup.Count);
        Assert.All(filesInBackup, f => Assert.False(f.IsDeleted));
    }

    [Fact(DisplayName = "04 - GetFilesInBackupAsync should include deleted files when DeletedAt matches backup")]
    public async Task GetFilesInBackupAsync_ShouldIncludeDeletedFilesWhenDeletedAtMatchesBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        // Insert file and mark as deleted at backup 2
        var file = FileModel.CreateNew("/backup/path", "folder1", "deleted_file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        // Insert version in backup 1
        var version = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        await sut.InsertVersionAsync(version, CancellationToken.None);

        // Mark as deleted at backup 2
        await sut.MarkAsDeletedAsync(new List<long> { fileId }, 2, CancellationToken.None);

        // Act
        var result = await sut.GetFilesInBackupAsync(2, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var files = result.Value.ToList();
        Assert.Single(files);
        Assert.True(files[0].IsDeleted);
    }

    [Fact(DisplayName = "05 - GetFilesWithVersionAsync should return files from specific backup path")]
    public async Task GetFilesWithVersionAsync_ShouldReturnFilesFromSpecificBackupPath()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        // Insert files in different backup paths
        var file1 = FileModel.CreateNew("/backup/path1", "folder1", "file1.txt");
        var file2 = FileModel.CreateNew("/backup/path2", "folder1", "file2.txt");

        var file1Id = (await sut.InsertAsync(file1, CancellationToken.None)).Value;
        var file2Id = (await sut.InsertAsync(file2, CancellationToken.None)).Value;

        // Insert versions
        var version1 = FileVersion.CreateNew(file1Id, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var version2 = FileVersion.CreateNew(file2Id, 1, true, 2048, "hash2", new byte[] { 3, 4 });

        await sut.InsertVersionAsync(version1, CancellationToken.None);
        await sut.InsertVersionAsync(version2, CancellationToken.None);

        // Act
        var result = await sut.GetFilesWithVersionAsync("/backup/path1", CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var files = result.Value.ToList();
        Assert.Single(files);
        Assert.Equal("/backup/path1", files[0].BackupPath);
    }

    [Fact(DisplayName = "06 - GetFilesWithVersionAsync should return latest hash from FileVersions")]
    public async Task GetFilesWithVersionAsync_ShouldReturnLatestHashFromFileVersions()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        // Insert multiple versions
        var version1 = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var version2 = FileVersion.CreateNew(fileId, 2, false, 1024, "hash2", new byte[] { 1, 2 });

        await sut.InsertVersionAsync(version1, CancellationToken.None);
        await sut.InsertVersionAsync(version2, CancellationToken.None);

        // Act
        var result = await sut.GetFilesWithVersionAsync("/backup/path", CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var files = result.Value.ToList();
        Assert.Single(files);
        Assert.Equal("hash2", files[0].CurrentHash);
    }

    [Fact(DisplayName = "07 - GetStreamOfFilesToRecoverAsync should return files with versions for specific backup")]
    public async Task GetStreamOfFilesToRecoverAsync_ShouldReturnFilesWithVersionsForSpecificBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var version1 = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var version2 = FileVersion.CreateNew(fileId, 2, false, 1024, "hash2", new byte[] { 3, 4 });

        await sut.InsertVersionAsync(version1, CancellationToken.None);
        await sut.InsertVersionAsync(version2, CancellationToken.None);

        // Act
        var stream = await sut.GetStreamOfFilesToRecoverAsync(2, CancellationToken.None);
        var filesToRecover = new List<string>();
        await foreach (var fileToRecover in stream)
        {
            filesToRecover.Add($"{fileToRecover.BackupPath}/{fileToRecover.RelativePath}/{fileToRecover.FileName}");
        }

        // Assert
        Assert.Single(filesToRecover);
    }

    [Fact(DisplayName = "08 - GetStreamOfFilesToRecoverAsync should get latest version lower or equal to backup")]
    public async Task GetStreamOfFilesToRecoverAsync_ShouldGetLatestVersionLowerOrEqualToBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file1 = FileModel.CreateNew("/backup/path", "folder1", "file1.txt");
        var file2 = FileModel.CreateNew("/backup/path", "folder1", "file2.txt");
        var file1Id = (await sut.InsertAsync(file1, CancellationToken.None)).Value;
        var file2Id = (await sut.InsertAsync(file2, CancellationToken.None)).Value;

        var file1Version1 = FileVersion.CreateNew(file1Id, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var file1Version2 = FileVersion.CreateNew(file1Id, 2, false, 1024, "hash2", new byte[] { 3, 4 });
        var file1Version3 = FileVersion.CreateNew(file1Id, 3, false, 1024, "hash3", new byte[] { 5, 6 });
        var file2Version1 = FileVersion.CreateNew(file2Id, 1, true, 1024, "hash4", new byte[] { 7, 8 });

        await sut.InsertVersionAsync(file1Version1, CancellationToken.None);
        await sut.InsertVersionAsync(file1Version2, CancellationToken.None);
        await sut.InsertVersionAsync(file1Version3, CancellationToken.None);
        await sut.InsertVersionAsync(file2Version1, CancellationToken.None);

        // Act - recover to backup 2
        var stream = await sut.GetStreamOfFilesToRecoverAsync(2, CancellationToken.None);
        var backupIds = new List<long>();
        await foreach (var fileToRecover in stream)
        {
            backupIds.Add(fileToRecover.VersionBackupId);
        }

        // Assert - should get version from backup 2, not backup 3
        Assert.Equal(2, backupIds.Count);
        Assert.Equal(2, backupIds[0]);
        Assert.Equal(1, backupIds[1]);
    }

    [Fact(DisplayName = "09 - GetFileContentAsync should return file content as byte array")]
    public async Task GetFileContentAsync_ShouldReturnFileContentAsByteArray()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var version = FileVersion.CreateNew(fileId, 1, true, content.Length, "hash1", content);
        var versionId = (await sut.InsertVersionAsync(version, CancellationToken.None)).Value;

        // Act
        var result = await sut.GetFileContentAsync(versionId, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(content, result.Value);
    }

    [Fact(DisplayName = "10 - GetFileContentAsync should return empty array when content is null")]
    public async Task GetFileContentAsync_ShouldReturnEmptyArrayWhenContentIsNull()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var version = FileVersion.CreateNew(fileId, 1, true, 0, "hash1", Array.Empty<byte>());
        var versionId = (await sut.InsertVersionAsync(version, CancellationToken.None)).Value;

        // Act
        var result = await sut.GetFileContentAsync(versionId, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }

    [Fact(DisplayName = "11 - GetFilesToDeleteAsync should return files and their versions from backup")]
    public async Task GetFilesToDeleteAsync_ShouldReturnFilesAndVersionsFromBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var version = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        await sut.InsertVersionAsync(version, CancellationToken.None);

        // Act
        var result = await sut.GetFilesToDeleteAsync(1, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var filesToDelete = result.Value.ToList();
        Assert.Single(filesToDelete);
        Assert.Equal(fileId, filesToDelete[0].FileId);
    }

    [Fact(DisplayName = "12 - GetFilesToDeleteAsync should indicate if file exists in next backup")]
    public async Task GetFilesToDeleteAsync_ShouldIndicateIfFileExistsInNextBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var version1 = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var version2 = FileVersion.CreateNew(fileId, 2, false, 1024, "hash1", new byte[] { 1, 2 });

        await sut.InsertVersionAsync(version1, CancellationToken.None);
        await sut.InsertVersionAsync(version2, CancellationToken.None);

        // Act
        var result = await sut.GetFilesToDeleteAsync(1, 2, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var filesToDelete = result.Value.ToList();
        Assert.Single(filesToDelete);
        Assert.True(filesToDelete[0].ExistsInNextBackup);
    }

    [Fact(DisplayName = "13 - MarkAsDeletedAsync should set IsDeleted to 1 and DeletedAt to backup id")]
    public async Task MarkAsDeletedAsync_ShouldSetIsDeletedAndDeletedAt()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        // Act
        var result = await sut.MarkAsDeletedAsync(new List<long> { fileId }, 5, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.True(result.Value > 0);

        // Verify
        var connection = databaseService.GetConnection();
        using var command = new SQLiteCommand(
            "SELECT IsDeleted, DeletedAt FROM Files WHERE Id = @id",
            connection);
        command.Parameters.AddWithValue("@id", fileId);
        
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(5, reader.GetInt64(1));
    }

    [Fact(DisplayName = "14 - MoveVersionsToBackupAsync should update BackupId for specified versions")]
    public async Task MoveVersionsToBackupAsync_ShouldUpdateBackupIdForVersions()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var version = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var versionId = (await sut.InsertVersionAsync(version, CancellationToken.None)).Value;

        // Act
        var result = await sut.MoveVersionsToBackupAsync(new List<long> { versionId }, 5, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.True(result.Value > 0);

        // Verify
        var connection = databaseService.GetConnection();
        using var command = new SQLiteCommand(
            "SELECT BackupId FROM FileVersions WHERE Id = @id",
            connection);
        command.Parameters.AddWithValue("@id", versionId);
        
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(5, reader.GetInt64(0));
    }

    [Fact(DisplayName = "15 - MoveDeletedFilesToNextBackupAsync should update DeletedAt for deleted files")]
    public async Task MoveDeletedFilesToNextBackupAsync_ShouldUpdateDeletedAtForDeletedFiles()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        var version = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        await sut.InsertVersionAsync(version, CancellationToken.None);

        // Mark as deleted at backup 2
        await sut.MarkAsDeletedAsync(new List<long> { fileId }, 2, CancellationToken.None);

        // Act
        var result = await sut.MoveDeletedFilesToNextBackupAsync(2, 3, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        // Verify
        var connection = databaseService.GetConnection();
        using var command = new SQLiteCommand(
            "SELECT DeletedAt FROM Files WHERE Id = @id",
            connection);
        command.Parameters.AddWithValue("@id", fileId);
        
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(3, reader.GetInt64(0));
    }

    [Fact(DisplayName = "16 - DeleteAllVersionsInBackupAsync should delete all versions from specified backup")]
    public async Task DeleteAllVersionsInBackupAsync_ShouldDeleteAllVersionsFromBackup()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file1 = FileModel.CreateNew("/backup/path", "folder1", "file1.txt");
        var file2 = FileModel.CreateNew("/backup/path", "folder2", "file2.txt");

        var file1Id = (await sut.InsertAsync(file1, CancellationToken.None)).Value;
        var file2Id = (await sut.InsertAsync(file2, CancellationToken.None)).Value;

        // Insert versions in backup 1
        var version1 = FileVersion.CreateNew(file1Id, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        var version2 = FileVersion.CreateNew(file2Id, 1, true, 1024, "hash2", new byte[] { 3, 4 });

        // Insert versions in backup 2
        var version3 = FileVersion.CreateNew(file1Id, 2, false, 1024, "hash1", new byte[] { 1, 2 });

        await sut.InsertVersionAsync(version1, CancellationToken.None);
        await sut.InsertVersionAsync(version2, CancellationToken.None);
        await sut.InsertVersionAsync(version3, CancellationToken.None);

        // Act
        var result = await sut.DeleteAllVersionsInBackupAsync(1, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(2, result.Value); // Should delete 2 versions from backup 1

        // Verify
        var connection = databaseService.GetConnection();
        using var command = new SQLiteCommand(
            "SELECT COUNT(*) FROM FileVersions WHERE BackupId = 1",
            connection);
        
        var countObj = await command.ExecuteScalarAsync();
        var count = countObj != null ? Convert.ToInt64(countObj) : 0L;
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "17 - DeleteFilesWithoutVersionsAsync should delete orphaned files")]
    public async Task DeleteFilesWithoutVersionsAsync_ShouldDeleteOrphanedFiles()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file1 = FileModel.CreateNew("/backup/path", "folder1", "file1.txt");
        var file2 = FileModel.CreateNew("/backup/path", "folder2", "file2.txt");

        var file1Id = (await sut.InsertAsync(file1, CancellationToken.None)).Value;
        var file2Id = (await sut.InsertAsync(file2, CancellationToken.None)).Value;

        // Only insert version for file1
        var version = FileVersion.CreateNew(file1Id, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        await sut.InsertVersionAsync(version, CancellationToken.None);

        // file2 has no versions, so it's orphaned

        // Act
        var result = await sut.DeleteFilesWithoutVersionsAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(1, result.Value); // Should delete 1 orphaned file (file2)

        // Verify
        var connection = databaseService.GetConnection();
        using var command = new SQLiteCommand(
            "SELECT COUNT(*) FROM Files WHERE Id = @id",
            connection);
        command.Parameters.AddWithValue("@id", file2Id);
        
        var countObj = await command.ExecuteScalarAsync();
        var count = countObj != null ? Convert.ToInt64(countObj) : 0L;
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "18 - GetFilesInBackupAsync should return empty collection when backup has no files")]
    public async Task GetFilesInBackupAsync_ShouldReturnEmptyCollectionWhenNoFiles()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        // Act
        var result = await sut.GetFilesInBackupAsync(1, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Empty(result.Value);
    }

    [Fact(DisplayName = "19 - Multiple inserts should handle auto-increment correctly")]
    public async Task MultipleInserts_ShouldHandleAutoIncrementCorrectly()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var ids = new List<long>();
        for (int i = 0; i < 5; i++)
        {
            var file = FileModel.CreateNew("/backup/path", $"folder{i}", $"file{i}.txt");
            var result = await sut.InsertAsync(file, CancellationToken.None);
            ids.Add(result.Value);
        }

        // Assert - IDs should be sequential and unique
        Assert.Equal(5, ids.Count);
        Assert.Equal(ids.Distinct().Count(), ids.Count);
        Assert.Equal(new[] { 1L, 2L, 3L, 4L, 5L }, ids);
    }

    [Fact(DisplayName = "20 - GetStreamOfFilesToRecoverAsync should handle deleted files correctly")]
    public async Task GetStreamOfFilesToRecoverAsync_ShouldHandleDeletedFilesCorrectly()
    {
        await using var databaseService = new InMemorySqliteDatabaseService();
        await CreateTablesAsync(databaseService.GetConnection());

        var sut = new FileRepository(databaseService);

        var file = FileModel.CreateNew("/backup/path", "folder1", "file.txt");
        var fileId = (await sut.InsertAsync(file, CancellationToken.None)).Value;

        // Insert version in backup 1
        var version1 = FileVersion.CreateNew(fileId, 1, true, 1024, "hash1", new byte[] { 1, 2 });
        await sut.InsertVersionAsync(version1, CancellationToken.None);

        // Mark as deleted at backup 3
        await sut.MarkAsDeletedAsync(new List<long> { fileId }, 3, CancellationToken.None);

        // Act - recover to backup 2 (before deletion)
        var stream = await sut.GetStreamOfFilesToRecoverAsync(2, CancellationToken.None);
        var files = new List<string>();
        await foreach (var recoveredFile in stream)
        {
            files.Add(recoveredFile.FileName);
        }

        // Assert - should return file because it wasn't deleted yet
        Assert.Single(files);
        Assert.Equal("file.txt", files[0]);
    }

    private static async Task CreateTablesAsync(SQLiteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS Files (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BackupPath TEXT NOT NULL,
                RelativePath TEXT NOT NULL,
                FileName TEXT NOT NULL,
                IsDeleted INTEGER DEFAULT 0,
                DeletedAt INTEGER
            );
            
            CREATE TABLE IF NOT EXISTS FileVersions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileId INTEGER NOT NULL,
                BackupId INTEGER NOT NULL,
                IsNew INTEGER NOT NULL,
                Size INTEGER NOT NULL,
                Hash TEXT NOT NULL,
                Content BLOB,
                FOREIGN KEY(FileId) REFERENCES Files(Id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_FileVersions_FileId ON FileVersions(FileId);
            CREATE INDEX IF NOT EXISTS idx_FileVersions_BackupId ON FileVersions(BackupId);
            CREATE INDEX IF NOT EXISTS idx_Files_BackupPath ON Files(BackupPath);";

        using var command = new SQLiteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}