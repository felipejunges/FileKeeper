namespace FileKeeper.Core.Models.DTOs;

public class FileToDelete
{
    public string RelativePath { get; private set; }
    public string StoredPath { get; private set; }

    public FileToDelete(string relativePath, string storedPath)
    {
        RelativePath = relativePath;
        StoredPath = storedPath;
    }
}