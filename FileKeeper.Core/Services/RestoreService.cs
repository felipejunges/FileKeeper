using System.Collections.Generic;
using System.Linq;
using ErrorOr;
using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using Spectre.Console;

namespace FileKeeper.Core.Services;

public class RestoreService
{
    private readonly IConfigurationService _configurationService;
    private readonly IAnsiConsole _console;
    private readonly IIndexService _indexService;

    public RestoreService(
        IConfigurationService configurationService,
        IAnsiConsole console,
        IIndexService indexService)
    {
        _configurationService = configurationService;
        _console = console;
        _indexService = indexService;
    }

    public async Task RestoreBackupAsync(string backupName, CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(configuration.DestinationDirectory))
        {
            _console.MarkupLine("[red]Configuration incomplete. Please set source and destination first.[/]");
            return;
        }
        
        var (backupIndex, _) = await _indexService.GetBackupIndexAsync(cancellationToken);
        var backupMetadata = backupIndex.Backups.FirstOrDefault(b => b.BackupName == backupName);
        
        if (backupMetadata is null)
        {
            _console.MarkupLine("[yellow]Backups not found.[/]");
            return;
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        
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
        
        var (backupIndex, _) = await _indexService.GetBackupIndexAsync(cancellationToken);
        
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