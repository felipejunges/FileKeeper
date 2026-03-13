using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Persistence.Repositories;

public class FileRepository : RepositoryBase, IFileRepository
{
    public FileRepository(IDatabaseService databaseService) : base(databaseService)
    {
    }

    public async Task<ErrorOr<IEnumerable<FileInBackupDM>>> GetFilesInBackupAsync(long backupId, CancellationToken token)
    {
        const string sql = @"
            SELECT 
                f.Id AS FileId,
                f.BackupPath,
                f.RelativePath,
                f.FileName,
                fv.Size AS FileSize,
                fv.Hash AS FileHash,
                fv.IsNew,
                0 AS IsDeleted
            FROM Files f
            INNER JOIN FileVersions fv ON f.Id = fv.FileId
            WHERE fv.BackupId = @backupId
            UNION ALL
            SELECT
                f.Id AS FileId,
                f.BackupPath,
                f.RelativePath,
                f.FileName,
                0 AS FileSize,
                '' AS FileHash,
                0 AS IsNew,
                f.IsDeleted
            FROM Files f
            WHERE f.DeletedAt = @backupId
            ORDER BY BackupPath, RelativePath, FileName;";

        return await QueryAsync<FileInBackupDM>(sql, new { backupId }, token);
    }

    public async Task<ErrorOr<IEnumerable<FileVersionDM>>> GetFilesWithVersionAsync(string backupPath, CancellationToken token)
    {
        const string sql = @"SELECT 
                f.Id,
                f.BackupPath,
                f.RelativePath,
                f.FileName,
                f.IsDeleted,
                fv.Hash AS CurrentHash
            FROM Files f
            LEFT JOIN FileVersions fv ON fv.Id = (
                SELECT Id 
                FROM FileVersions 
                WHERE FileId = f.Id 
                ORDER BY BackupId DESC 
                LIMIT 1
            )
            WHERE
                f.BackupPath = @backupPath
                AND IsDeleted = 0;";

        return await QueryAsync<FileVersionDM>(sql, new { backupPath }, token);
    }

    public async Task<IAsyncEnumerable<FileToRecoverDM>> GetStreamOfFilesToRecoverAsync(long backupId, CancellationToken token)
    {
        const string sql = @"SELECT 
                f.Id,
                f.BackupPath,
                f.RelativePath,
                f.FileName,
                fv.Size,
                fv.BackupId AS VersionBackupId,
                fv.Id AS VersionId
            FROM Files f
            LEFT JOIN FileVersions fv ON fv.Id = (
                SELECT Id 
                FROM FileVersions 
                WHERE FileId = f.Id
                AND BackupId <= @backupId
                ORDER BY BackupId DESC 
                LIMIT 1
            )
            WHERE (
                f.IsDeleted = 0
                OR f.IsDeleted = 1 AND f.DeletedAt > @backupId
            )
            ORDER BY f.BackupPath, f.RelativePath;";
        
        return await StreamQueryAsync<FileToRecoverDM>(sql, new { backupId }, token);
    }

    public async Task<ErrorOr<byte[]>> GetFileContentAsync(long fileVersionId, CancellationToken token)
    {
        try
        {
            const string sql = "SELECT Content FROM FileVersions WHERE Id = @id;";
            
            var result = await QuerySingleOrDefaultAsync<byte[]>(sql, new { id = fileVersionId }, token);
            
            if (result.IsError)
                return result.Errors;

            return result.Value ?? new byte[0];
        }
        catch (Exception ex)
        {
            return Error.Failure("repository.query_failed", $"Query execution failed: {ex.Message}");
        }
    }

    public async Task<ErrorOr<IEnumerable<FileToDeleteDM>>> GetFilesToDeleteAsync(long backupId, long? nextBackupId, CancellationToken token)
    {
        const string sql = @"
            SELECT
                fv1.Id,
                fv1.FileId,
                fv1.BackupId,
                fv1.IsNew,
                fv1.Size,
                CASE
                    WHEN fv2.Id IS NOT NULL THEN 1
                    ELSE 0
                END AS ExistsInNextBackup,
                f.BackupPath,
                f.RelativePath,
                f.FileName
            FROM FileVersions fv1
            INNER JOIN Files f ON fv1.FileId = f.Id
            LEFT JOIN FileVersions fv2
                ON fv1.FileId = fv2.FileId
                AND fv2.BackupId = @nextBackupId
            WHERE fv1.BackupId = @backupId
            AND (f.DeletedAt IS NULL OR f.DeletedAt != @nextBackupId);";
        
        return await QueryAsync<FileToDeleteDM>(sql, new { backupId, nextBackupId }, token);
    }

    public async Task<ErrorOr<long>> InsertAsync(FileModel fileModel, CancellationToken token)
    {
        const string sql = @$"
            INSERT INTO Files (
                {nameof(FileModel.BackupPath)},
                {nameof(FileModel.RelativePath)},
                {nameof(FileModel.FileName)},
                {nameof(FileModel.IsDeleted)},
                {nameof(FileModel.DeletedAt)})
            VALUES (@BackupPath, @RelativePath, @FileName, @IsDeleted, @DeletedAt);
            SELECT last_insert_rowid() AS Id;";

        var result = await QuerySingleOrDefaultAsync<long>(sql, fileModel, token);

        if (result.IsError)
            return result;
        
        fileModel.UpdateId(result.Value);
        
        return result.Value;
    }
    
    public async Task<ErrorOr<long>> InsertVersionAsync(FileVersion version, CancellationToken token)
    {
        const string sql = @$"
            INSERT INTO FileVersions (
                {nameof(FileVersion.FileId)},
                {nameof(FileVersion.BackupId)},
                {nameof(FileVersion.IsNew)},
                {nameof(FileVersion.Size)},
                {nameof(FileVersion.Hash)},
                {nameof(FileVersion.Content)})
            VALUES (@FileId, @BackupId, @IsNew, @Size, @Hash, @Content);
            SELECT last_insert_rowid() AS Id;";

        var result = await QuerySingleOrDefaultAsync<long>(sql, version, token);

        if (result.IsError)
            return result;
        
        version.UpdateId(result.Value);
        
        return result.Value;
    }

    public async Task<ErrorOr<int>> MarkAsDeletedAsync(List<long> idsFilesToMarkAsDeleted, long backupId, CancellationToken token)
    {
        const string sql = @"
            UPDATE Files
            SET IsDeleted = 1, DeletedAt = @backupId
            WHERE Id IN @ids;";

        return await ExecuteAsync(sql, new { ids = idsFilesToMarkAsDeleted, backupId }, token);
    }

    public Task<ErrorOr<int>> MoveVersionsToBackupAsync(List<long> idsVersionsToMove, long backupId, CancellationToken token)
    {
        const string sql = @"
            UPDATE FileVersions
            SET BackupId = @backupId
            WHERE Id IN @ids;";

        return ExecuteAsync(sql, new { ids = idsVersionsToMove, backupId }, token);
    }

    public Task<ErrorOr<int>> MoveDeletedFilesToNextBackupAsync(long sourceBackupId, long destinationBackupId, CancellationToken token)
    {
        const string sql = @"
            UPDATE FILES
            SET DeletedAt = @destinationBackupId
            WHERE DeletedAt = @sourceBackupId;";

        return ExecuteAsync(sql, new { sourceBackupId, destinationBackupId }, token);
    }
    
    public Task<ErrorOr<int>> DeleteAllVersionsInBackupAsync(long backupId, CancellationToken token)
    {
        const string sql = @"
            DELETE FROM FileVersions
            WHERE BackupId = @backupId;";

        return ExecuteAsync(sql, new { backupId }, token);
    }

    public Task<ErrorOr<int>> DeleteFilesWithoutVersionsAsync(CancellationToken token)
    {
        const string sql = @"
            DELETE FROM Files
            WHERE Id NOT IN (SELECT DISTINCT FileId FROM FileVersions);";

        return ExecuteAsync(sql, null, token);
    }
}