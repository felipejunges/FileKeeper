using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Core.Models;
using FileKeeper.Core.Utils;

namespace FileKeeper.Core.Services.Abstraction;

public class FileSourceService : IFileSourceService
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileInfoBuilder _fileInfoBuilder;

    public FileSourceService(
        IFileSystem fileSystem,
        IFileInfoBuilder fileInfoBuilder)
    {
        _fileSystem = fileSystem;
        _fileInfoBuilder = fileInfoBuilder;
    }

    public async Task<List<FileMetadata>> ScanLocalFolderAsync(string sourceDir, IEnumerable<string> excludePatterns, CancellationToken cancellationToken)
    {
        var result = new List<FileMetadata>();

        // Materialize excludePatterns to avoid multiple enumeration
        var excludeList = excludePatterns.ToList();

        var sourceDirBase64 = EncodingUtils.ToBase64(sourceDir);
        var files = _fileSystem.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var f in files)
        {
            var relPath = Path.GetRelativePath(sourceDir, f);

            if (excludeList.Any(p => relPath.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            
            // TODO: toda aquela papagaiada do IFileInfo existe só pra pegar o Length e o LastWriteTimeUtc??
            var info = _fileInfoBuilder.Build(f);

            result.Add(new FileMetadata
            {
                RelativePath = relPath,
                StoredPath = Path.Combine(sourceDirBase64, relPath),
                Size = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                Hash = await _fileSystem.ComputeHashAsync(Path.Combine(sourceDir, relPath), cancellationToken) // Isso pode deixar bem lento...
            });
        }

        return result;
    }
}