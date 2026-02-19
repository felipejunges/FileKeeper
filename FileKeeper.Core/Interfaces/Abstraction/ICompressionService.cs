using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.Abstraction;

public interface ICompressionService
{
    Task CompressFilesAsync(IList<(string FullPath, string StoredPath)> files, string backupPath, string backupName,
        CancellationToken cancellationToken);

    Task DecompressFilesAsync(IList<(string BackupName, string StoredPath)> files, string backupPath, string destinationPath,
        CancellationToken cancellationToken);

    Task MoveFileAsync(string backupPath, string originBackupName, string destinatioBackupName,
        List<(string OriginStoredPath, string DestinationStoredPath)> files, CancellationToken cancellationToken);

    Task<string?> ReadFileContentAsync(string backupPath, string storedPath, CancellationToken cancellationToken);

    Task WriteFileContentAsync(string backupPath, string storedPath, string content, CancellationToken cancellationToken);

    Task RemoveFolderAsync(string backupPath, string firstBackupBackupName, CancellationToken cancellationToken);

    Task<List<string>> GetEntriesAsync(string backupPath, CancellationToken cancellationToken);
}