namespace FileKeeper.UI.Models;

public record BackupFileVersionViewModel(
    long FileId,
    string FullPath,
    long FileSize,
    string FileHash);