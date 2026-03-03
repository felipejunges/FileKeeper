using FileKeeper.Core.Interfaces.Services;

namespace FileKeeper.Core.Services;

public class LocalFileSystem : IFileSystem
{
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);

    public FileStream GetReadFileStream(string fileName)
    {
        return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public bool DirectoryExists(string destinationFolder) =>
        Directory.Exists(destinationFolder);
    
    public void CreateDirectory(string destinationFolder) =>
        Directory.CreateDirectory(destinationFolder);

    public bool IsDirectoryEmpty(string destinationFolder) =>
        !Directory.EnumerateFileSystemEntries(destinationFolder).Any();
}