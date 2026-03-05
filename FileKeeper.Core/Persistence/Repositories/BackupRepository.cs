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
                {nameof(Backup.DeletedFiles)}
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

    public async Task<ErrorOr<Backup>> GetNextBackupAfterAsync(DateTime createdAt, CancellationToken token)
    {
        const string sql = @$"
            SELECT
                {nameof(Backup.Id)},
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)}
            FROM Backups
            WHERE CreatedAt > @createdAt
            ORDER BY CreatedAt ASC
            LIMIT 1;";

        var result = await QuerySingleOrDefaultAsync<Backup>(sql, new { createdAt }, token);

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
                {nameof(Backup.DeletedFiles)}
            FROM Backups
            ORDER BY {nameof(Backup.CreatedAt)} DESC;";

        return await QueryAsync<Backup>(sql, null, token);
    }

    public async Task<ErrorOr<long>> InsertAsync(Backup backup, CancellationToken token)
    {
        const string sql = @$"
            INSERT INTO Backups (
                {nameof(Backup.CreatedAt)},
                {nameof(Backup.CreatedFiles)},
                {nameof(Backup.UpdatedFiles)},
                {nameof(Backup.DeletedFiles)})
            VALUES (
                @CreatedAt,
                @CreatedFiles,
                @UpdatedFiles,
                @DeletedFiles);
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
                {nameof(Backup.DeletedFiles)} = @DeletedFiles
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
                {nameof(Backup.DeletedFiles)}
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
}