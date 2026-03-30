namespace FileKeeper.Core.Models.DTOs;

public class FileToSave()
{
    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }
    public required string StoredPath { get; init; }
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public required DateTime LastModified { get; init; }
    public string FoundInSnapshot { get; set; } = string.Empty;
}