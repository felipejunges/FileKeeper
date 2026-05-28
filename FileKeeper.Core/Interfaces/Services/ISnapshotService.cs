using ErrorOr;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.Core.Interfaces.Services;

public interface ISnapshotService
{
    Task<ErrorOr<SnapshotIndex>> GetIndexAsync(CancellationToken token);
    
    Task<ErrorOr<Success>> SaveIndexAsync(SnapshotIndex index, CancellationToken token);

    Task<ErrorOr<Success>> AddFileAsync(FileToSave file, CancellationToken token);
    
    Task<ErrorOr<Success>> AddFilesAsync(IEnumerable<FileToSave> files, CancellationToken token);

    Task<ErrorOr<Success>> RestoreFileAsync(FileToRestore file, CancellationToken token);
    
    Task<ErrorOr<Success>> RestoreFilesAsync(IEnumerable<FileToRestore> files, CancellationToken token);
    
    Task<ErrorOr<Success>> DeleteFileAsync(string entryPath, CancellationToken token);
}