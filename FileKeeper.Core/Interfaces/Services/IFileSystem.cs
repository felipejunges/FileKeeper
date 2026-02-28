namespace FileKeeper.Core.Interfaces.Services;

public interface IFileSystem
{
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    FileStream GetReadFileStream(string fileName);
}