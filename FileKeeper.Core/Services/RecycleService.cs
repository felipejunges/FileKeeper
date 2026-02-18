using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using Spectre.Console;

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
        var backupIndex = await _indexService.GetBackupIndexAsync(cancellationToken);
        
        // 2. Check how many backups needs to be merged
        var removeCount = backupIndex.Backups.Count - configuration.KeepMaxBackups;
        
        if (removeCount <= 0)
        {
            _console.MarkupLine($"[grey]Recycling skipped: Current Backups {backupIndex.Backups.Count} is less than or equal to Max Backups {configuration.KeepMaxBackups}[/]");
            return;
        }

        var orderedBackups = backupIndex.Backups.OrderBy(b => b.CreatedAtUtc).ToList();
        
        // 3. Merge the backup files
        for (int i = 0; i < removeCount; i++)
        {
            // TODO: pensar: não executar nada definitivo dentro do loop, para possibilitar o cancelamento do processo*
            cancellationToken.ThrowIfCancellationRequested();
            
            var firstBackup = orderedBackups[i];
            var nextBackup = orderedBackups[i + 1];

            var filesToMove = nextBackup.Files.Where(f => f.FoundInBackup == firstBackup.BackupName).ToList();

            // 3.1. Merge the files from the backup to be removed to the next backup
            foreach (var fileToMove in filesToMove)
            {
                var firstBackupFile = firstBackup.Files.FirstOrDefault(f => f.IsSameFile(fileToMove));
                if (firstBackupFile == null)
                    continue;

                // TODO: * continuando, talvez nao chamar arquivo por arquivo, mas uma lista de arquivos
                await _compressionService.MoveFileAsync(
                    configuration.DestinationDirectory,
                    firstBackup.BackupName,
                    firstBackupFile.StoredPath,
                    nextBackup.BackupName,
                    fileToMove.StoredPath,
                    cancellationToken);
                
                // 3.2. Update the file metadata in the index to point to the new backup
                var allBackupsWithFile = orderedBackups
                    .Where(b => b.CreatedAtUtc > firstBackup.CreatedAtUtc)
                    .SelectMany(b => b.Files)
                    .Where(f => f.IsSameFile(fileToMove))
                    .Where(f => f.FoundInBackup == firstBackup.BackupName)
                    .ToList();
                
                allBackupsWithFile.ForEach(f => f.FoundInBackup = nextBackup.BackupName);
            }
            
            // 3.3. Delete old folder
            await _compressionService.RemoveFolderAsync(configuration.DestinationDirectory, firstBackup.BackupName, cancellationToken);
            
            // 3.4. Remove the backup metadata from the index
            backupIndex.Backups.Remove(firstBackup);
        }
        
        // 4. Save the File Index
        await _indexService.SaveBackupIndexAsync(backupIndex, cancellationToken);
    }
}