using System.Text.Json.Serialization;

namespace FileKeeper.Core.Models;

public class BackupIndex
{
    public List<BackupMetadata> Backups { get; set; } = new List<BackupMetadata>();
}

public class BackupMetadata
{
    public required string BackupName { get; init; } = string.Empty;
    public required DateTime CreatedAtUtc { get; init; }
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    
    private readonly List<FileMetadata> _files = new List<FileMetadata>();
    
    [JsonIgnore]
    public IReadOnlyList<FileMetadata> Files => _files.AsReadOnly();
    
    [JsonInclude]
    [JsonPropertyName("Files")]
    public List<FileMetadata> FilesSerialization
    {
        get => _files;
        private set
        {
            _files.Clear();
            _files.AddRange(value);
        }
    }

    public void AddFile(FileMetadata file)
    {
        _files.Add(file);
        FileCount++;
        TotalSize += file.Size;
    }
}

public class FileMetadata
{
    public string RelativePath { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string FoundInBackup { get; set; } = string.Empty;
}