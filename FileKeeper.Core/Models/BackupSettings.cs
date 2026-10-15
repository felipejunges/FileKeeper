namespace FileKeeper.Core.Models;

/// <summary>
/// User configuration for a backup job.
/// </summary>
public sealed class BackupSettings
{
    /// <summary>Folders whose content is backed up.</summary>
    public List<string> SourceFolders { get; set; } = [];

    /// <summary>The zip file that holds every snapshot.</summary>
    public string DestinationFile { get; set; } = string.Empty;

    /// <summary>
    /// Folders to skip. An absolute path ignores that exact folder (and everything below it);
    /// a plain name (e.g. "node_modules") ignores every folder with that name.
    /// </summary>
    public List<string> IgnoredFolders { get; set; } = [];

    /// <summary>
    /// Optional size limit of the destination file. When exceeded after a backup,
    /// the oldest snapshots are pruned (merged into the next one) until it fits.
    /// </summary>
    public long? MaxBackupSizeBytes { get; set; }

    public BackupSettings Clone() => new()
    {
        SourceFolders = [.. SourceFolders],
        DestinationFile = DestinationFile,
        IgnoredFolders = [.. IgnoredFolders],
        MaxBackupSizeBytes = MaxBackupSizeBytes,
    };
}
