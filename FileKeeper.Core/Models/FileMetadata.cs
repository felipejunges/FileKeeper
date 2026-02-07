namespace FileKeeper.Core.Models;

public class FileMetadata
{
    public string RelativePath { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty; // Path within the backup archive (prefixed with folder name)
    public string Hash { get; set; } = string.Empty; // SHA256 or MD5
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string FoundInBackup { get; set; } = string.Empty; // Name of the backup folder where the content is stored
}