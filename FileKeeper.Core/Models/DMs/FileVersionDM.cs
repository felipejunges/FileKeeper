namespace FileKeeper.Core.Models.DMs;

public class FileVersionDM
{
    public required long Id { get; init; }
    public required string BackupPath { get; init; }
    public required string RelativePath { get; init; }
    public required string FileName { get; init; }
    public required string CurrentHash { get; init; }
}