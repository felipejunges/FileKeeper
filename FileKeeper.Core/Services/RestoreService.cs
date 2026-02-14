using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using Spectre.Console;

namespace FileKeeper.Core.Services;

public class RestoreService
{
    private readonly IConfigurationService _configurationService;
    private readonly IAnsiConsole _console;
    private readonly IIndexService _indexService;
    private readonly ICompressionService _compressionService;

    public RestoreService(
        IConfigurationService configurationService,
        IAnsiConsole console,
        IIndexService indexService,
        ICompressionService compressionService)
    {
        _configurationService = configurationService;
        _console = console;
        _indexService = indexService;
        _compressionService = compressionService;
    }

    public async Task RestoreBackupAsync(string backupName, string destinationPath, CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(configuration.DestinationDirectory))
        {
            _console.MarkupLine("[red]Configuration incomplete. Please set source and destination first.[/]");
            return;
        }
        
        var backupIndex = await _indexService.GetBackupIndexAsync(cancellationToken);
        var backupMetadata = backupIndex.Backups.FirstOrDefault(b => b.BackupName == backupName);
        
        if (backupMetadata is null)
        {
            _console.MarkupLine("[yellow]Backups not found.[/]");
            return;
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
         _console.MarkupLine($"[green]Restoring backup:[/] {backupMetadata.BackupName}");
         
         var files = new List<(string BackupName, string StoredPath, string RelativePath)>();

         foreach (var file in backupMetadata.Files)
         {
             if (file.FoundInBackup == backupMetadata.BackupName)
             {
                 files.Add((backupMetadata.BackupName, file.StoredPath, file.RelativePath));
             }
             else
             {
                 // search for the file info in the related backup
                 var foundFile = backupIndex
                     .Backups.FirstOrDefault(b => b.BackupName == file.FoundInBackup)
                     ?.Files.FirstOrDefault(f => f.StoredPath == file.StoredPath);
                 
                 if (foundFile is not null)
                     files.Add((file.FoundInBackup, foundFile.StoredPath, foundFile.RelativePath));
             }
         }

         await _compressionService.DecompressFilesAsync(
             files,
             configuration.DestinationDirectory,
             destinationPath,
             cancellationToken);
    }
    
    public async Task<IList<(string BackupName, DateTime CreatedAtUtc)>> GetListOfBackupsAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(configuration.DestinationDirectory))
        {
            _console.MarkupLine("[red]Configuration incomplete. Please set source and destination first.[/]");
            return new List<(string BackupName, DateTime CreatedAtUtc)>();
        }
        
        var backupIndex = await _indexService.GetBackupIndexAsync(cancellationToken);
        
        if (backupIndex.Backups.Count == 0)
        {
            _console.MarkupLine("[yellow]No backups found.[/]");
            return new List<(string BackupName, DateTime CreatedAtUtc)>();
        }

        return backupIndex.Backups
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => (b.BackupName, b.CreatedAtUtc))
            .ToList();
    }
}