namespace FileKeeper.Core.Models.DTOs;

public class FileToDelete
{
    public string StoredPath { get; private set; }

    public FileToDelete(string storedPath)
    {
        StoredPath = storedPath;
    }
}