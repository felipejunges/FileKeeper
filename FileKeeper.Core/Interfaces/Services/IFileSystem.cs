namespace FileKeeper.Core.Interfaces.Services;

public interface IFileSystem
{
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    FileStream GetReadFileStream(string fileName);
    bool DirectoryExists(string destinationFolder);
    void CreateDirectory(string destinationFolder);
    bool IsDirectoryEmpty(string destinationFolder);
}