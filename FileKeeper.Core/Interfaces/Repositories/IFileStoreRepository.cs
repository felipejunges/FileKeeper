namespace FileKeeper.Core.Interfaces.Repositories;

public interface IFileStoreRepository
{
    Task AddFileAsync(string sourceFilePath, string entryPath, CancellationToken token);
    Task AddFileAsync(IEnumerable<string> sourceFilePaths, string entryPath, CancellationToken token);
    Task AddStreamAsync(Stream sourceStream, string entryPath, CancellationToken token);
    Task<Stream> GetFileContentStreamAsync(string entryPath, CancellationToken token);
    Task ExtractFileAsync(string entryPath, string destinationFilePath, CancellationToken token);
    Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token);
    Task DeleteFileAsync(string entryPath, CancellationToken token);
}