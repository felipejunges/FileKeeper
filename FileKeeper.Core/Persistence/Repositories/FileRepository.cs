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

    public async Task<ErrorOr<FileInBackupDM>> GetFilesInBackupAsync(long backupId, CancellationToken token)
    {
        
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

    public async Task<ErrorOr<IEnumerable<FileToRecoverDM>>> GetFilesToRecoverAsync(long backupId, CancellationToken token)
    {
        const string sql = @$"SELECT 
                f.Id,
                f.BackupPath,
                f.RelativePath,
                f.FileName,
                fv.BackupId,
                fv.Size,
                fv.Hash,
                fv.Content
            FROM Files f
            INNER JOIN FileVersions fv ON f.Id = fv.FileId
            WHERE (
                f.IsDeleted = 0
                OR f.IsDeleted = 1 AND f.DeletedAt < @backupId
            )
            AND fv.BackupId = (
                SELECT MAX(fv2.BackupId)
                FROM FileVersions fv2
                WHERE fv2.FileId = f.Id
                    AND fv2.BackupId <= @backupId
            )
            ORDER BY f.BackupPath, f.RelativePath;";
        
        return await QueryAsync<FileToRecoverDM>(sql, new { backupId }, token);
    }

    public async Task<ErrorOr<IEnumerable<FileToDeleteDM>>> GetFilesToDeleteAsync(long backupId, long? nextBackupId, CancellationToken token)
    {
        const string sql = @"
            SELECT
                fv1.Id,
                fv1.FileId,
                fv1.BackupId,
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
            VALUES (@FileId, @BackupId, @IsNew, @Size, @Hash, @CompressedContent);
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