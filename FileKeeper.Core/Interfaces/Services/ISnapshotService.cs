using ErrorOr;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Services;

public interface ISnapshotService
{
    Task<ErrorOr<SnapshotIndex>> GetIndexAsync(CancellationToken token);
    
    Task<ErrorOr<Success>> SaveIndexAsync(SnapshotIndex index, CancellationToken token);

    //[Obsolete("Use the List version", true)]
    Task<ErrorOr<Success>> AddFileAsync(FileToSave file, CancellationToken token);
    
    Task<ErrorOr<Success>> AddFilesAsync(IEnumerable<FileToSave> files, IProgress<BackupProgress>? progress, CancellationToken token);

    [Obsolete("Use the List version", true)]
    Task<ErrorOr<Success>> RestoreFileAsync(FileToRestore file, CancellationToken token);
    
    Task<ErrorOr<Success>> RestoreFilesAsync(IEnumerable<FileToRestore> files, IProgress<BackupProgress>? progress, CancellationToken token);
    
    [Obsolete("Use the List version", true)]
    Task<ErrorOr<Success>> DeleteFileAsync(FileToDelete file, CancellationToken token);

    Task<ErrorOr<Success>> DeleteFilesAsync(IEnumerable<FileToDelete> files, CancellationToken token);

    Task<ErrorOr<Success>> RemoveEmptyDirectoriesAsync(CancellationToken token);
}