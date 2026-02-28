using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Models;

namespace FileKeeper.Core.Persistence.Repositories;

public interface IFileVersionRepository
{
    Task<ErrorOr<FileVersion>> GetByIdAsync(string id, CancellationToken token);
    Task<ErrorOr<List<FileVersion>>> GetByFileIdAsync(string fileId, CancellationToken token);
    Task<ErrorOr<FileVersion>> GetLatestVersionAsync(string fileId, CancellationToken token);
    Task<ErrorOr<Success>> InsertAsync(FileVersion version, CancellationToken token);
    Task<ErrorOr<Success>> UpdateAsync(FileVersion version, CancellationToken token);
    Task<ErrorOr<Success>> DeleteAsync(string id, CancellationToken token);
    Task<ErrorOr<int>> GetNextVersionNumberAsync(string fileId, CancellationToken token);
}

public class FileVersionRepository : RepositoryBase, IFileVersionRepository
{
    public FileVersionRepository(IDatabaseService databaseService) : base(databaseService)
    {
    }

    public async Task<ErrorOr<FileVersion>> GetByIdAsync(string id, CancellationToken token)
    {
        const string sql = @"
            SELECT Id, FileId, Content, Hash, Size, VersionNumber, CreatedAt
            FROM FileVersions
            WHERE Id = @id;";

        var result = await QuerySingleOrDefaultAsync<FileVersion>(sql, new { id }, token);

        if (result.IsError)
            return result.Errors;

        if (result.Value is null)
            return Error.NotFound("version.not_found", $"Version with id {id} not found");

        return result.Value;
    }

    public async Task<ErrorOr<List<FileVersion>>> GetByFileIdAsync(string fileId, CancellationToken token)
    {
        const string sql = @"
            SELECT Id, FileId, Content, Hash, Size, VersionNumber, CreatedAt
            FROM FileVersions
            WHERE FileId = @fileId
            ORDER BY VersionNumber DESC;";

        return await QueryAsync<FileVersion>(sql, new { fileId }, token);
    }

    public async Task<ErrorOr<FileVersion>> GetLatestVersionAsync(string fileId, CancellationToken token)
    {
        const string sql = @"
            SELECT Id, FileId, Content, Hash, Size, VersionNumber, CreatedAt
            FROM FileVersions
            WHERE FileId = @fileId
            ORDER BY VersionNumber DESC
            LIMIT 1;";

        var result = await QuerySingleOrDefaultAsync<FileVersion>(sql, new { fileId }, token);

        if (result.IsError)
            return result.Errors;

        if (result.Value is null)
            return Error.NotFound("version.not_found", $"No versions found for file {fileId}");

        return result.Value;
    }

    public async Task<ErrorOr<Success>> InsertAsync(FileVersion version, CancellationToken token)
    {
        const string sql = @"
            INSERT INTO FileVersions (Id, FileId, Content, Hash, Size, VersionNumber, CreatedAt)
            VALUES (@Id, @FileId, @Content, @Hash, @Size, @VersionNumber, @CreatedAt);";

        var result = await ExecuteAsync(sql, version, token);

        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<Success>> UpdateAsync(FileVersion version, CancellationToken token)
    {
        const string sql = @"
            UPDATE FileVersions
            SET FileId = @FileId, Content = @Content, Hash = @Hash, Size = @Size, VersionNumber = @VersionNumber, CreatedAt = @CreatedAt
            WHERE Id = @Id;";

        var result = await ExecuteAsync(sql, version, token);

        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<Success>> DeleteAsync(string id, CancellationToken token)
    {
        const string sql = "DELETE FROM FileVersions WHERE Id = @id;";

        var result = await ExecuteAsync(sql, new { id }, token);

        return result.IsError ? result.Errors : Result.Success;
    }

    public async Task<ErrorOr<int>> GetNextVersionNumberAsync(string fileId, CancellationToken token)
    {
        const string sql = "SELECT COALESCE(MAX(VersionNumber), 0) as MaxVersion FROM FileVersions WHERE FileId = @fileId;";

        var result = await QuerySingleOrDefaultAsync<dynamic>(sql, new { fileId }, token);

        if (result.IsError)
            return result.Errors;

        int maxVersion = result.Value?.MaxVersion ?? 0;
        return maxVersion + 1;
    }
}