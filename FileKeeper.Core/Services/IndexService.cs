using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using System.Text.Json;

namespace FileKeeper.Core.Services;

public class IndexService : IIndexService
{
    private readonly IConfigurationService _configurationService;
    private readonly ICompressionService _compressionService;

    public IndexService(
        IConfigurationService configurationService,
        ICompressionService compressionService)
    {
        _configurationService = configurationService;
        _compressionService = compressionService;
    }

    public async Task<(BackupIndex, string)> GetBackupIndexAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        
        var backupPath = Path.Combine(configuration.DestinationDirectory, "backup.zip"); // TODO: think about the file extension (maybe just the file 'name' (without the extension))
        var backupIndexContent = await _compressionService.ReadFileContentAsync(backupPath, "index.json", cancellationToken);
        var backupIndex = backupIndexContent != null
            ? JsonSerializer.Deserialize<BackupIndex>(backupIndexContent) ?? new BackupIndex()
            : new BackupIndex();

        return (backupIndex, backupPath);
    }
}