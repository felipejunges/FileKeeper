namespace FileKeeper.Core.Models.DMs;

public class FileToDeleteDM
{
    public long Id { get; set; }
    public long FileId { get; set; }
    public long BackupId { get; set; }
    public bool IsNew { get; set; }
    public long Size { get; set; }
    public bool ExistsInNextBackup { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}