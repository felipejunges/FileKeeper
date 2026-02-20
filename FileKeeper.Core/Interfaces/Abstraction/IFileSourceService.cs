using FileKeeper.Core.Models;

namespace FileKeeper.Core.Interfaces.Abstraction;

public interface IFileSourceService
{
    Task<List<FileMetadata>> ScanLocalFolderAsync(string sourceDir, IEnumerable<string> excludePatterns, CancellationToken cancellationToken);
}