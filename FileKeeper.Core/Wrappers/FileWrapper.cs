using FileKeeper.Core.Interfaces.Wrappers;
using System.Security.Cryptography;

namespace FileKeeper.Core.Wrappers;

public class FileWrapper : IFileWrapper
{
    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public Stream Create(string path)
    {
        return File.Create(path);
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);

    public async Task<(long Size, DateTime LastModified, string Hash)> GetFileMetadataAsync(string path, CancellationToken token)
    {
        var info = new FileInfo(path);

        await using var stream = info.OpenRead();
        var hashBytes = await SHA256.HashDataAsync(stream, token);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return (
            Size: info.Length,
            LastModified: info.LastWriteTimeUtc,
            Hash: hash);
    }
    
    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public bool DirectoryIsEmpty(string path)
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }

    public void CreateDirectoryIfNotExists(string dir)
    {
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public void DeleteDirectory(string path)
    {
        Directory.Delete(path, true);
    }
}