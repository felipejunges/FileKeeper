namespace FileKeeper.Core.Models;

/// <summary>
/// Represents a version of a file.
/// </summary>
public class FileVersion
{
    public string Id { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public byte[]? Content { get; set; }
    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }
    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

