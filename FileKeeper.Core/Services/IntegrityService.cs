using ErrorOr;
using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using FileKeeper.Core.Utils;
using Spectre.Console;

namespace FileKeeper.Core.Services;

public class IntegrityService : IIntegrityService
{
    private readonly IAnsiConsole _console;
    private readonly IConfigurationService _configurationService;
    private readonly ICompressionService _compressionService;
    private readonly IIndexService _indexService;
    private readonly IFileSourceService _fileSourceService;

    public IntegrityService(
        IAnsiConsole console,
        IConfigurationService configurationService,
        ICompressionService compressionService,
        IIndexService indexService,
        IFileSourceService fileSourceService)
    {
        _console = console;
        _configurationService = configurationService;
        _compressionService = compressionService;
        _indexService = indexService;
        _fileSourceService = fileSourceService;
    }

    public async Task<ErrorOr<Success>> VerifyCompressedFilesIntegrityAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        if (string.IsNullOrEmpty(configuration.DestinationDirectory))
        {
            _console.MarkupLine("[red]Destination directory not set. Integrity check aborted.[/]");
            return Error.Failure(description: "Destination directory not set.");
        }

        var backupIndex = await _indexService.GetBackupIndexAsync(cancellationToken);
        if (backupIndex.Backups.Count == 0)
        {
            _console.MarkupLine("[yellow]No backups found in index.[/]");
            return Result.Success;
        }

        _console.MarkupLine("[bold blue]Starting Integrity Verification...[/]");

        // 1. Collect all expected entries from index
        var expectedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var backup in backupIndex.Backups)
        {
            foreach (var file in backup.FilesSerialization)
            {
                var entryPath = Path.Combine(file.FoundInBackup, file.StoredPath).Replace('\\', '/');
                expectedEntries.Add(entryPath);
            }
        }

        // 2. Load actual entries from zip
        var actualEntriesList = await _compressionService.GetEntriesAsync(configuration.DestinationDirectory, cancellationToken);
        var actualEntries = actualEntriesList
            .Select(e => e.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3. Compare
        var missingFiles = expectedEntries.Except(actualEntries).ToList();
        var extraFiles = actualEntries.Except(expectedEntries)
            .Where(e => !e.Equals("index.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!missingFiles.Any() && !extraFiles.Any())
        {
            _console.MarkupLine("[bold green]Integrity Verification Successful![/] Index completely matches files stored in the archives.");
        }
        else
        {
            if (missingFiles.Any())
            {
                _console.MarkupLine($"[bold red]CRITICAL:[/] Found {missingFiles.Count} missing files referenced in index:");
                foreach (var missing in missingFiles.Take(10))
                    _console.MarkupLine($"  - [red]{missing}[/]");
                if (missingFiles.Count > 10) _console.MarkupLine("  - ...");
            }

            if (extraFiles.Any())
            {
                _console.MarkupLine($"[bold yellow]WARNING:[/] Found {extraFiles.Count} files in archives not referenced by any backup point:");
                foreach (var extra in extraFiles.Take(10))
                    _console.MarkupLine($"  - [yellow]{extra}[/]");
                if (extraFiles.Count > 10) _console.MarkupLine("  - ...");
            }
        }

        return Result.Success;
    }
    
    public async Task<ErrorOr<Success>> VerifyLocalFilesDifferencesAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);
        if (string.IsNullOrEmpty(configuration.DestinationDirectory))
        {
            _console.MarkupLine("[red]Destination directory not set. Integrity check aborted.[/]");
            return Error.Failure(description: "Destination directory not set.");
        }

        var backupIndex = await _indexService.GetBackupIndexAsync(cancellationToken);
        if (backupIndex.Backups.Count == 0)
        {
            _console.MarkupLine("[yellow]No backups found in index.[/]");
            return Result.Success;
        }

        _console.MarkupLine("[bold blue]Starting Integrity Verification...[/]");

        // 1. 
        var allStoredPaths = backupIndex.Backups
            .SelectMany(b => b.FilesSerialization)
            .Select(f => f.StoredPath.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _console.MarkupLine("");
        _console.MarkupLine("[bold blue]Checking Local Filesystem synchronization...[/]");

        foreach (var sourceDir in configuration.SourceDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceDirBase64 = EncodingUtils.ToBase64(sourceDir);
            var currentFiles = await _fileSourceService.ScanLocalFolderAsync(sourceDir, configuration.ExcludePatterns, cancellationToken);

            var actualStoredPaths = currentFiles
                .Select(f => f.StoredPath.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var expectedStoredPaths = allStoredPaths
                .Where(p => p.StartsWith(sourceDirBase64, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingLocally = expectedStoredPaths.Except(actualStoredPaths).ToList();
            var untrackedLocally = actualStoredPaths.Except(expectedStoredPaths).ToList();

            _console.MarkupLine($"[bold]Source:[/] [cyan]{sourceDir}[/]");

            if (!missingLocally.Any() && !untrackedLocally.Any())
            {
                _console.MarkupLine("  [green]Local files are in sync with index.[/]");
            }
            else
            {
                if (missingLocally.Any())
                {
                    _console.MarkupLine($"  [yellow]Missing locally:[/] {missingLocally.Count} files found in index but not on disk (will be skipped in next backup):");
                    foreach (var missing in missingLocally.Take(5))
                    {
                        var relPath = missing.Length > sourceDirBase64.Length ? missing.Substring(sourceDirBase64.Length).TrimStart('/') : missing;
                        _console.MarkupLine($"    - [yellow]{relPath}[/]");
                    }
                    if (missingLocally.Count > 5) _console.MarkupLine("    - ...");
                }

                if (untrackedLocally.Any())
                {
                    _console.MarkupLine($"  [blue]Untracked locally:[/] {untrackedLocally.Count} files on disk but not in index (will be added in next backup):");
                    foreach (var untracked in untrackedLocally.Take(5))
                    {
                        var relPath = untracked.Length > sourceDirBase64.Length ? untracked.Substring(sourceDirBase64.Length).TrimStart('/') : untracked;
                        _console.MarkupLine($"    - [blue]{relPath}[/]");
                    }
                    if (untrackedLocally.Count > 5) _console.MarkupLine("    - ...");
                }
            }
        }

        return Result.Success;
    }
}
