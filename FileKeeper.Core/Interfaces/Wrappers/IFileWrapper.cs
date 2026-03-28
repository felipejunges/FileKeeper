namespace FileKeeper.Core.Interfaces.Wrappers;

public interface IFileWrapper
{
    bool Exists(string path);
    Stream OpenRead(string path);
    Stream Create(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
}