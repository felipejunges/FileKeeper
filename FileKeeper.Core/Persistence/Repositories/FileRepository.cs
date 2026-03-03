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
                {nameof(FileVersion.Size)},
                {nameof(FileVersion.Hash)},
                {nameof(FileVersion.Content)})
            VALUES (@FileId, @BackupId, @Size, @Hash, @CompressedContent);
            SELECT last_insert_rowid() AS Id;";

        var result = await QuerySingleOrDefaultAsync<long>(sql, version, token);

        if (result.IsError)
            return result;
        
        version.UpdateId(result.Value);
        
        return result.Value;
    }

    public async Task<ErrorOr<int>> MarkAsDeletedAsync(List<long> idsArquivosExcluir, long backupId, CancellationToken token)
    {
        const string sql = @"
            UPDATE Files
            SET IsDeleted = 1, DeletedAt = @backupId
            WHERE Id IN @ids;";

        return await ExecuteAsync(sql, new { ids = idsArquivosExcluir, backupId }, token);
    }
}