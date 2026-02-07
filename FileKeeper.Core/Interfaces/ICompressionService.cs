using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces;

public interface ICompressionService
{
    Task CompressFilesAsync(IList<(string FullPath, string StoredPath)> files, string backupPath, string backupName, CancellationToken cancellationToken);
    
    Task<string?> ReadFileContentAsync(string backupPath, string storedPath, CancellationToken cancellationToken);
    
    Task WriteFileContentAsync(string backupPath, string storedPath, string content, CancellationToken cancellationToken);
}