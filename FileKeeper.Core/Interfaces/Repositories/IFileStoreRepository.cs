using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface IFileStoreRepository
{
    Task AddFileAsync(FileToSave file, CancellationToken token);
    Task AddFilesAsync(IEnumerable<FileToSave> files, IProgress<BackupProgress>? progress, CancellationToken token);
    Task AddStreamAsync(Stream sourceStream, string entryPath, CancellationToken token);
    Task<Stream> GetFileContentStreamAsync(string entryPath, CancellationToken token);
    Task ExtractFileAsync(FileToRestore file, CancellationToken token);
    Task ExtractFilesAsync(IEnumerable<FileToRestore> file, IProgress<BackupProgress>? progress, CancellationToken token);
    Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token);
    Task DeleteFileAsync(string entryPath, CancellationToken token);
}