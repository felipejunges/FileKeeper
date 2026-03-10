using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Persistence.Repositories;

public class BackupRepository : RepositoryBase, IBackupRepository
{
    public BackupRepository(IDatabaseService databaseService) : base(databaseService)
    {
    }

    public async Task<ErrorOr<Backup>> GetByIdAsync(long id, CancellationToken token)
    {
        const string sql = @$"
            SELECT
                {nameof(Backup.Id)},
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)},
                {nameof(Backup.TotalSize)}
            FROM Backups
            WHERE Id = @id;";

        var result = await QuerySingleOrDefaultAsync<Backup>(sql, new { id }, token);

        if (result.IsError)
            return result.Errors;

        if (result.Value is null)
            return Error.NotFound(description: "Backup not found.");

        return result.Value;
    }

    public Task<ErrorOr<int>> GetCountAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM Backups;";

        return QuerySingleOrDefaultAsync<int>(sql, null, cancellationToken);
    }

    public async Task<ErrorOr<Backup>> GetNextBackupByIdAsync(long backupId, CancellationToken token)
    {
        const string sql = @$"
            SELECT
                {nameof(Backup.Id)},
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)},
                {nameof(Backup.TotalSize)}
            FROM Backups
            WHERE Id > @backupId
            ORDER BY Id ASC
            LIMIT 1;";

        var result = await QuerySingleOrDefaultAsync<Backup>(sql, new { backupId }, token);

        if (result.IsError)
            return result.Errors;

        if (result.Value is null)
            return Error.NotFound(description: "Backup not found.");

        return result.Value;
    }

    public async Task<ErrorOr<IEnumerable<Backup>>> GetAllAsync(CancellationToken token)
    {
        const string sql = @$"
            SELECT
                {nameof(Backup.Id)},
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)},
                {nameof(Backup.TotalSize)}
            FROM Backups
            ORDER BY {nameof(Backup.CreatedAt)} DESC;";

        return await QueryAsync<Backup>(sql, null, token);
    }

    public Task<ErrorOr<long>> GetAllBackupsTotalSizeAsync(CancellationToken token)
    {
        const string sql = "SELECT SUM(TotalSize) FROM Backups;";
        
        return QuerySingleOrDefaultAsync<long>(sql, null, token);
    }
    
    public async Task<ErrorOr<long>> InsertAsync(Backup backup, CancellationToken token)
    {
        const string sql = @$"
            INSERT INTO Backups (
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)},
                {nameof(Backup.TotalSize)})
            VALUES (
                @CreatedAt,
                @CreatedFiles,
                @UpdatedFiles,
                @DeletedFiles,
                @TotalSize);
            SELECT last_insert_rowid() AS Id;";

        var result = await QuerySingleOrDefaultAsync<long>(sql, backup, token);

        if (result.IsError)
            return result;

        backup.UpdateId(result.Value);

        return result.Value;
    }

    public Task UpdateAsync(Backup backup, CancellationToken token)
    {
        const string sql = @$"
            UPDATE Backups
            SET {nameof(Backup.CreatedFiles)} = @CreatedFiles,
                {nameof(Backup.UpdatedFiles)} = @UpdatedFiles,
                {nameof(Backup.DeletedFiles)} = @DeletedFiles,
                {nameof(Backup.TotalSize)} = @TotalSize
            WHERE Id = @Id;";

        return ExecuteAsync(sql, backup, token);
    }

    public Task<ErrorOr<int>> DeleteAsync(long backupId, CancellationToken token)
    {
        const string sql = @"
            DELETE FROM Backups
            WHERE Id = @backupId;";

        return ExecuteAsync(sql, new { backupId }, token);
    }

    public async Task<ErrorOr<Backup>> GetOldestAsync(CancellationToken cancellationToken)
    {
        const string sql = @$"
            SELECT
                {nameof(Backup.Id)},
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)},
                {nameof(Backup.TotalSize)}
            FROM Backups
            ORDER BY CreatedAt ASC
            LIMIT 1;";

        var backupResult = await QuerySingleOrDefaultAsync<Backup>(sql, null, cancellationToken);

        if (backupResult.IsError)
            return backupResult.Errors;

        if (backupResult.Value is null)
            return Error.NotFound(description: "No backups found.");

        return backupResult.Value;
    }

    public async Task<ErrorOr<Success>> IncrementMovedFilesDataToBackupAsync(long backupId, IEnumerable<long> movedIds, CancellationToken token)
    {
        const string sql = "SELECT IsNew, Size FROM FileVersions WHERE Id IN @movedIds;";

        var newFilesResult = await QueryAsync<(bool IsNew, long Size)>(sql, new { movedIds }, token);
        if (newFilesResult.IsError)
            return newFilesResult.Errors;

        var sizeToAdd = newFilesResult.Value.Sum(f => f.Size);
        var createdFilesToAdd = newFilesResult.Value.Count(f => f.IsNew);
        var updatedFilesToAdd = newFilesResult.Value.Count(f => !f.IsNew);

        const string updateSql = @$"
            UPDATE Backups
            SET {nameof(Backup.CreatedFiles)} = {nameof(Backup.CreatedFiles)} + @createdFilesToAdd,
                {nameof(Backup.UpdatedFiles)} = {nameof(Backup.UpdatedFiles)} + @updatedFilesToAdd,
                {nameof(Backup.TotalSize)} = {nameof(Backup.TotalSize)} + @sizeToAdd
            WHERE Id = @backupId;";

        var updateResult = await ExecuteAsync(updateSql, new { backupId, createdFilesToAdd, updatedFilesToAdd, sizeToAdd }, token);
        if (updateResult.IsError) 
            return updateResult.Errors;

        return Result.Success;
    }
}