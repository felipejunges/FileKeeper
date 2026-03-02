using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;
using File = FileKeeper.Core.Models.Entities.File;

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
                f.BackupPath = @backupPath;";

        return await QueryAsync<FileVersionDM>(sql, new { backupPath }, token);
    }
    
    public async Task<ErrorOr<long>> InsertAsync(File file, CancellationToken token)
    {
        const string sql = @$"
            INSERT INTO Files (
                {nameof(File.BackupPath)},
                {nameof(File.RelativePath)},
                {nameof(File.FileName)},
                {nameof(File.IsDeleted)},
                {nameof(File.DeletedAt)})
            VALUES (@BackupPath, @RelativePath, @FileName, @IsDeleted, @DeletedAt);
            SELECT last_insert_rowid() AS Id;";

        var result = await QuerySingleOrDefaultAsync<long>(sql, file, token);

        if (result.IsError)
            return result;
        
        file.UpdateId(result.Value);
        
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
            VALUES (@FileId, @BackupId, @Size, @Hash, @Content);
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