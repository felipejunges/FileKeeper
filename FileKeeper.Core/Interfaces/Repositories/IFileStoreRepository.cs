using ErrorOr;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface IFileStoreRepository
{
    Task<ErrorOr<Success>> AddFileAsync(FileToSave file, CancellationToken token);
    Task<ErrorOr<Success>> AddFilesAsync(IEnumerable<FileToSave> files, IProgress<BackupProgress>? progress, CancellationToken token);
    Task AddStreamAsync(Stream sourceStream, string entryPath, CancellationToken token);
    Task<Stream> GetFileContentStreamAsync(string entryPath, CancellationToken token);
    Task ExtractFileAsync(FileToRestore file, CancellationToken token);
    Task ExtractFilesAsync(IEnumerable<FileToRestore> file, IProgress<BackupProgress>? progress, CancellationToken token);
    Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token);
    Task<ErrorOr<Deleted>> DeleteFileAsync(FileToDelete file, CancellationToken token);
    Task<ErrorOr<Deleted>> DeleteFilesAsync(IEnumerable<FileToDelete> files, CancellationToken token);
    Task RemoveEmptyDirectoriesAsync(CancellationToken token);
}