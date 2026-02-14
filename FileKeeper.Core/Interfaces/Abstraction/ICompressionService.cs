using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.Abstraction;

public interface ICompressionService
{
    Task CompressFilesAsync(IList<(string FullPath, string StoredPath)> files, string backupPath, string backupName,
        CancellationToken cancellationToken);
    
    Task DecompressFilesAsync(IList<(string BackupName, string StoredPath, string RelativePath)> files, string backupPath, string destinationPath,
        CancellationToken cancellationToken);
    
    Task<string?> ReadFileContentAsync(string backupPath, string storedPath, CancellationToken cancellationToken);
    
    Task WriteFileContentAsync(string backupPath, string storedPath, string content, CancellationToken cancellationToken);

    Task MoveFileAsync(string backupPath, string originStoredPath, string destinationStoredPath, CancellationToken cancellationToken);
    
    Task RemoveFolderAsync(string backupPath, string firstBackupBackupName, CancellationToken cancellationToken);
}