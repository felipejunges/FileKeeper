namespace FileKeeper.Core.Models.DTOs;

public class FileToSave
{
    public string FullPath { get; private set; }
    public string RelativePath { get; private set; }
    public string StoredPath { get; private set; }
    public string Hash { get; private set; }
    public long Size { get; private set; }
    public DateTime LastModified { get; private set; }
    public string FoundInSnapshot { get; private set; }
    
    public FileToSave(string fullPath, string relativePath, string storedPath, string hash, long size, DateTime lastModified)
    {
        FullPath = fullPath;
        RelativePath = relativePath;
        StoredPath=storedPath;
        Hash = hash;
        Size = size;
        LastModified = lastModified;
        FoundInSnapshot = string.Empty;
    }

    public void UpdateFoundIn(string foundInSnapshot)
    {
        FoundInSnapshot = foundInSnapshot;
    }
    
    public void UpdateFoundInAndPath(string foundInSnapshot, string storedPath)
    {
        FoundInSnapshot = foundInSnapshot;
        StoredPath = storedPath;
    }
}