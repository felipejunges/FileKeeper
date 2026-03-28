using FileKeeper.Core.Interfaces.Wrappers;

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
    
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);
}

