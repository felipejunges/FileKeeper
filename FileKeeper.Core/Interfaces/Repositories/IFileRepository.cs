using ErrorOr;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface IFileRepository
{
    Task<ErrorOr<IEnumerable<FileVersionDM>>> GetFilesWithVersionAsync(string backupPath, CancellationToken token);
    Task<ErrorOr<IEnumerable<FileToRecoverDM>>> GetFilesToRecoverAsync(long backupId, CancellationToken token);
    Task<ErrorOr<IEnumerable<FileToDeleteDM>>> GetFilesToDeleteAsync(long backupId, long? nextBackupId, CancellationToken token);
    Task<ErrorOr<long>> InsertAsync(FileModel fileModel, CancellationToken token);
    Task<ErrorOr<long>> InsertVersionAsync(FileVersion version, CancellationToken token);
    Task<ErrorOr<int>> MarkAsDeletedAsync(List<long> idsFilesToMarkAsDeleted, long backupId, CancellationToken token);
    Task<ErrorOr<int>> MoveVersionsToBackupAsync(List<long> idsVersionsToMove, long backupId, CancellationToken token);
    Task<ErrorOr<int>> DeleteAllVersionsInBackupAsync(long backupId, CancellationToken token);
    Task<ErrorOr<int>> DeleteFilesWithoutVersionsAsync(CancellationToken token);
}