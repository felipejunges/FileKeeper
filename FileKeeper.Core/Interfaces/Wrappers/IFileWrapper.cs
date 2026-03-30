namespace FileKeeper.Core.Interfaces.Wrappers;

public interface IFileWrapper
{
    bool Exists(string path);
    Stream OpenRead(string path);
    Stream Create(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    Task<(long Size, DateTime LastModified, string Hash)> GetFileMetadataAsync(string path, CancellationToken token);
}