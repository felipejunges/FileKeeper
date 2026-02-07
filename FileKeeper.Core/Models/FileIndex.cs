namespace FileKeeper.Core.Models;

public class FileIndex
{
    public string BackupName { get; set; } = string.Empty; // Timestamp
    public DateTime CreatedAtUtc { get; set; }
    public List<FileMetadata> Files { get; set; } = new List<FileMetadata>();
}