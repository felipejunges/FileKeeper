namespace FileKeeper.Core.Models.DMs;

public class FileToRecoverDM
{
    public long Id { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
}