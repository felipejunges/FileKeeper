namespace FileKeeper.Core.Models.DTOs;

public class FileToRestore
{
    public string FullPath { get; private set; }
    public string StoredPath { get; private set; }

    public FileToRestore(string fullPath, string storedPath)
    {
        FullPath = fullPath;
        StoredPath = storedPath;
    }
}