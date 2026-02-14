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

    public async Task<BackupIndex> GetBackupIndexAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        
        var backupIndexContent = await _compressionService.ReadFileContentAsync(
            configuration.DestinationDirectory,
            "index.json",
            cancellationToken);
        
        var backupIndex = backupIndexContent != null
            ? JsonSerializer.Deserialize<BackupIndex>(backupIndexContent) ?? new BackupIndex()
            : new BackupIndex();

        return backupIndex;
    }
}