using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;
using File = FileKeeper.Core.Models.Entities.File;

namespace FileKeeper.Core.Persistence.Repositories;

public interface IFileRepository
{
    Task<ErrorOr<IEnumerable<FileVersionDM>>> GetFilesWithVersionAsync(string path, CancellationToken token);
    Task<ErrorOr<long>> InsertAsync(File file, CancellationToken token);
    Task<ErrorOr<long>> InsertVersionAsync(FileVersion version, CancellationToken token);
    Task<ErrorOr<int>> MarkAsDeletedAsync(List<long> idsArquivosExcluir, long backupId, CancellationToken token);
    
    
    Task<ErrorOr<File>> GetByIdAsync(string id, CancellationToken token);
    Task<ErrorOr<IEnumerable<File>>> GetAllAsync(CancellationToken token);
    Task<ErrorOr<Success>> UpdateAsync(File file, CancellationToken token);
    Task<ErrorOr<Success>> DeleteAsync(string id, CancellationToken token);
    Task<ErrorOr<bool>> ExistsByPathAsync(string path, CancellationToken token);
}

public class FileRepository : RepositoryBase, IFileRepository
{
    public FileRepository(IDatabaseService databaseService) : base(databaseService)
    {
    }

    public async Task<ErrorOr<IEnumerable<FileVersionDM>>> GetFilesWithVersionAsync(string path, CancellationToken token)
    {
        const string sql = @"SELECT 
                f.Id,
                f.BackupPath,
                f.RelativePath,
                f.IsDeleted,
                fv.Hash AS CurrentHash
            FROM Files f
            LEFT JOIN FileVersions fv ON fv.Id = (
                SELECT Id 
                FROM FileVersions 
                WHERE FileId = f.Id 
                ORDER BY BackupId DESC 
                LIMIT 1
            );";

        return await QueryAsync<FileVersionDM>(sql, new { path }, token);
    }
    
    public async Task<ErrorOr<long>> InsertAsync(File file, CancellationToken token)
    {
        const string sql = @$"
            INSERT INTO Files (
                {nameof(File.BackupPath)},
                {nameof(File.RelativePath)},
                {nameof(File.IsDeleted)},
                {nameof(File.DeletedAt)})
            VALUES (@BackupPath, @RelativePath, @IsDeleted, @DeletedAt);
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

    public async Task<ErrorOr<File>> GetByIdAsync(string id, CancellationToken token)
    {
        const string sql = "SELECT Id, Path, Name, IsDeleted FROM Files WHERE Id = @id;";

        var result = await QuerySingleOrDefaultAsync<File>(sql, new { id }, token);

        if (result.IsError)
            return result.Errors;

        if (result.Value is null)
            return Error.NotFound("file.not_found", $"File with id {id} not found");
        
        return result.Value;
    }

    public async Task<ErrorOr<IEnumerable<File>>> GetAllAsync(CancellationToken token)
    {
        const string sql = "SELECT Id, Path, Name, IsDeleted FROM Files WHERE IsDeleted = 0;";

        return await QueryAsync<File>(sql, token: token);
    }

    public async Task<ErrorOr<Success>> UpdateAsync(File file, CancellationToken token)
    {
        const string sql = @"
            UPDATE Files
            SET Path = @Path, Name = @Name, IsDeleted = @IsDeleted
            WHERE Id = @Id;";

        var result = await ExecuteAsync(sql, file, token);

        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<Success>> DeleteAsync(string id, CancellationToken token)
    {
        const string sql = "DELETE FROM Files WHERE Id = @id;";
        
        var result = await ExecuteAsync(sql, new { id }, token);
        
        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<bool>> ExistsByPathAsync(string path, CancellationToken token)
    {
        const string sql = "SELECT COUNT(*) as Count FROM Files WHERE Path = @path AND IsDeleted = 0;";

        var result = await QuerySingleOrDefaultAsync<dynamic>(sql, new { path }, token);

        if (result.IsError)
            return result.Errors;

        return result.Value?.Count > 0;
    }
}

