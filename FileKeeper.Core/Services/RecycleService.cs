using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using Spectre.Console;
using System.Text.Json;

namespace FileKeeper.Core.Services;

public class RecycleService : IRecycleService
{
    private readonly IAnsiConsole _console;
    private readonly IConfigurationService _configurationService;
    private readonly ICompressionService _compressionService;
    private readonly IIndexService _indexService;
    
    public RecycleService(
        IAnsiConsole console,
        IConfigurationService configurationService,
        ICompressionService compressionService,
        IIndexService indexService)
    {
        _console = console;
        _configurationService = configurationService;
        _compressionService = compressionService;
        _indexService = indexService;
    }

    public async Task RecycleBackupsAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);

        if (configuration.KeepMaxBackups <= 0)
        {
            _console.MarkupLine($"[grey]Recycling skipped: Max Backups is {configuration.KeepMaxBackups}[/]");
            return;
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        // 1. Get The Backup Index
        var (backupIndex, backupPath) = await _indexService.GetBackupIndexAsync(cancellationToken);
        
        // 2. Check how many backups needs to be merged
        var removeCount = backupIndex!.Backups.Count - configuration.KeepMaxBackups;

        var orderedBackups = backupIndex.Backups.OrderBy(b => b.CreatedAtUtc).ToList();
        
        // 3. Merge the backup files
        for (int i = 0; i < removeCount; i++)
        {
            // TODO: pensar: não executar nada definitivo dentro do loop, para possibilitar o cancelamento do processo
            cancellationToken.ThrowIfCancellationRequested();
            
            var firstBackup = orderedBackups[i];
            var nextBackup = orderedBackups[i + 1];

            // 3.1. Merge the files from the backup to be removed to the next backup
            foreach (var nextBackupFile in nextBackup.Files)
            {
                // Se o arquivo não se encontra no backup a ser removido, não precisa fazer nada
                if (nextBackupFile.FoundInBackup != firstBackup.BackupName)
                    continue;
                
                var firstBackupFile = firstBackup.Files.FirstOrDefault(f => f.IsSameFile(nextBackupFile));
                if (firstBackupFile == null)
                    continue;

                await _compressionService.MoveFileAsync(
                    backupPath,
                    firstBackupFile.StoredPath,
                    nextBackupFile.StoredPath,
                    cancellationToken);
                
                nextBackupFile.FoundInBackup = nextBackup.BackupName;
            }
            
            // 3.2. Delete old folder
            await _compressionService.RemoveFolderAsync(backupPath, firstBackup.BackupName, cancellationToken);
        }
        
        // 4. Save the File Index
        var indexJson = JsonSerializer.Serialize(backupIndex, new JsonSerializerOptions { WriteIndented = true });
        await _compressionService.WriteFileContentAsync(backupPath, "index.json", indexJson, cancellationToken);
    }
}