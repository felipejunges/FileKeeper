namespace FileKeeper.Core.Models.DMs;

public class FileInBackupDM
{
    public required long FileId { get; init; }
    public required string BackupPath { get; init; }
    public required string RelativePath { get; init; }
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required string FileHash { get; init; }
    public required bool IsNew { get; init; }
    public required bool IsDeleted { get; init; }
    
    public string FullFileName => Path.Combine(BackupPath, RelativePath, FileName);
}