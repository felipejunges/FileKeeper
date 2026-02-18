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
        
        var filesToMove = new List<(string OriginStoredPath, string DestinationStoredPath)>();
        
        // 3. Create the list of backup files to move
        for (int i = 0; i < removeCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var backupToDelete = orderedBackups[i];
            var backupToBeMerged = orderedBackups[i + 1];

            var filesToCheck = backupToBeMerged.Files.Where(f => f.FoundInBackup == backupToDelete.BackupName).ToList();

            // 3.1. Check the files from the backup to be removed to the next backup
            foreach (var fileToCheck in filesToCheck)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var firstBackupFile = backupToDelete.Files.FirstOrDefault(f => f.IsSameFile(fileToCheck));
                if (firstBackupFile == null)
                    continue;
                
                filesToMove.Add((firstBackupFile.StoredPath, fileToCheck.StoredPath));

                // 3.2. Update the file metadata in the index to point to the new backup
                var allBackupsWithFile = orderedBackups
                    .Where(b => b.CreatedAtUtc > backupToDelete.CreatedAtUtc)
                    .SelectMany(b => b.Files)
                    .Where(f => f.IsSameFile(fileToCheck))
                    .Where(f => f.FoundInBackup == backupToDelete.BackupName)
                    .ToList();
                
                allBackupsWithFile.ForEach(f => f.FoundInBackup = backupToBeMerged.BackupName);
            }
            
            // 3.3. Move files in the compressed file
            await _compressionService.MoveFileAsync(
                configuration.DestinationDirectory,
                backupToDelete.BackupName,
                backupToBeMerged.BackupName,
                filesToMove,
                cancellationToken);
            
            // 3.4. Delete old folder
            await _compressionService.RemoveFolderAsync(configuration.DestinationDirectory, backupToDelete.BackupName, cancellationToken);
            
            // 3.5. Remove the backup metadata from the index
            backupIndex.Backups.Remove(backupToDelete);
        }
        
        // 4. Save the File Index
        await _indexService.SaveBackupIndexAsync(backupIndex, cancellationToken);
    }
}