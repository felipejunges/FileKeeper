namespace FileKeeper.Core.Models.DTOs;

public class FileToSave
{
    public string FullPath { get; private set; } = string.Empty;
    
    public string RelativePath { get; private set; } = string.Empty;
    
    public string StoredPath { get; private set; } = string.Empty;

    public string Hash { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public DateTime LastModified { get; private set; }

    public string FoundInSnapshot { get; set; } = string.Empty;
}