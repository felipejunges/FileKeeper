using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Persistence.Repositories;

public interface IFileRepository
{
    Task<ErrorOr<IEnumerable<FileVersionDM>>> GetFilesWithVersionAsync(string path, CancellationToken token);
    Task<ErrorOr<Success>> InsertAsync(FileModel file, CancellationToken token);
    Task<ErrorOr<Success>> InsertVersionAsync(FileVersion version, CancellationToken token);
    Task<ErrorOr<Success>> MarkAsDeletedAsync(List<string> idsArquivosExcluir, long newVersionNumber, CancellationToken token);
    
    
    Task<ErrorOr<FileModel>> GetByIdAsync(string id, CancellationToken token);
    Task<ErrorOr<IEnumerable<FileModel>>> GetAllAsync(CancellationToken token);
    Task<ErrorOr<Success>> UpdateAsync(FileModel file, CancellationToken token);
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
        const string sql = @"SELECT Id, Path, Name, IsDeleted
            FROM Files WHERE Path = @path
            AND DeletedVersionNumber IS NULL";

        return await QueryAsync<FileVersionDM>(sql, new { path }, token);
    }
    
    public async Task<ErrorOr<Success>> InsertAsync(FileModel file, CancellationToken token)
    {
        const string sql = @"
            INSERT INTO Files (Id, Path, Name, IsDeleted)
            VALUES (@Id, @Path, @Name, @IsDeleted);";

        var result = await ExecuteAsync(sql, file, token);
        
        return result.IsError ? result.Errors : Result.Success;
    }
    
    public async Task<ErrorOr<Success>> InsertVersionAsync(FileVersion version, CancellationToken token)
    {
        const string sql = @"
            INSERT INTO FileVersions (Id, FileId, Content, Hash, Size, VersionNumber, CreatedAt)
            VALUES (@Id, @FileId, @Content, @Hash, @Size, @VersionNumber, @CreatedAt);";

        var result = await ExecuteAsync(sql, version, token);

        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<Success>> MarkAsDeletedAsync(List<string> idsArquivosExcluir, long deletedVersionNumber, CancellationToken token)
    {
        const string sql = @"
            UPDATE Files
            SET IsDeleted = 1, DeletedVersionNumber = @deletedVersionNumber
            WHERE Id IN @ids;";

        var result = await ExecuteAsync(sql, new { ids = idsArquivosExcluir, deletedVersionNumber }, token);

        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<FileModel>> GetByIdAsync(string id, CancellationToken token)
    {
        const string sql = "SELECT Id, Path, Name, IsDeleted FROM Files WHERE Id = @id;";

        var result = await QuerySingleOrDefaultAsync<FileModel>(sql, new { id }, token);

        if (result.IsError)
            return result.Errors;

        if (result.Value is null)
            return Error.NotFound("file.not_found", $"File with id {id} not found");
        
        return result.Value;
    }

    public async Task<ErrorOr<IEnumerable<FileModel>>> GetAllAsync(CancellationToken token)
    {
        const string sql = "SELECT Id, Path, Name, IsDeleted FROM Files WHERE IsDeleted = 0;";

        return await QueryAsync<FileModel>(sql, token: token);
    }

    public async Task<ErrorOr<Success>> UpdateAsync(FileModel file, CancellationToken token)
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

