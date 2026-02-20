using ErrorOr;
using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Models;
using Spectre.Console;

namespace FileKeeper.Core.Services;

public class BackupService
{
    private readonly IAnsiConsole _console;
    private readonly IConfigurationService _configurationService;
    private readonly ICompressionService _compressionService;
    private readonly IRecycleService _recycleService;
    private readonly IIndexService _indexService;
    private readonly IFileSourceService _fileSourceService;

    public BackupService(
        IAnsiConsole console,
        IConfigurationService configurationService,
        ICompressionService compressionService,
        IRecycleService recycleService,
        IIndexService indexService,
        IFileSourceService fileSourceService)
    {
        _console = console;
        _configurationService = configurationService;
        _compressionService = compressionService;
        _recycleService = recycleService;
        _indexService = indexService;
        _fileSourceService = fileSourceService;
    }

    public async Task<ErrorOr<Success>> CreateBackupAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.LoadAsync(cancellationToken);

        if (string.IsNullOrEmpty(configuration.DestinationDirectory) || configuration.SourceDirectories.Count == 0)
        {
            _console.MarkupLine("[red]Configuration incomplete. Please set source and destination first.[/]");
            return Error.Failure(description: "Configuration incomplete. Please set source and destination first.");
        }

        _console.MarkupLine("[bold yellow]Starting Backup...[/]");

        var backupName = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        // 1. Get The Backup Index and Previous File Index
        var backupIndex = await _indexService.GetBackupIndexAsync(cancellationToken);

        var lastBackupMetadata = backupIndex.Backups.OrderByDescending(b => b.CreatedAtUtc).FirstOrDefault();

        cancellationToken.ThrowIfCancellationRequested();

        // 2. Create the new Index Structure
        var newBackupMetadata = new BackupMetadata()
        {
            BackupName = backupName,
            CreatedAtUtc = DateTime.UtcNow
        };

        backupIndex.Backups.Add(newBackupMetadata);

        // 3. Scan All Sources
        var filesToZip = new System.Collections.Concurrent.ConcurrentBag<(string FullPath, string StoredPath)>();

        var metadataLock = new object();

        foreach (var sourceDir in configuration.SourceDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentFiles = await _fileSourceService.ScanLocalFolderAsync(sourceDir, configuration.ExcludePatterns, cancellationToken);

            Parallel.ForEach(currentFiles, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, (file, _) =>
            {
                var existing = lastBackupMetadata?.Files.FirstOrDefault(f => f.IsSameFile(file));

                if (existing != null)
                {
                    if (existing.Hash == file.Hash)
                    {
                        // CASO: O arquivo já existe, e o hash é o mesmo: então podemos reaproveitar o arquivo do backup anterior
                        file.FoundInBackup = existing.FoundInBackup;
                    }
                    else
                    {
                        // CASO: O arquivo já existe, mas o hash é diferente: então precisamos atualizar o arquivo do backup
                        file.FoundInBackup = backupName;
                        filesToZip.Add((Path.Combine(sourceDir, file.RelativePath), file.StoredPath));
                    }
                }
                else
                {
                    // CASO: O arquivo é novo: então precisamos adicionar ao backup
                    file.FoundInBackup = backupName;
                    filesToZip.Add((Path.Combine(sourceDir, file.RelativePath), file.StoredPath));
                }

                lock (metadataLock)
                {
                    newBackupMetadata.AddFile(file);
                }
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 4. Create Zip
        if (filesToZip.Any())
        {
            // ConcurrentBag<T> is not an IList<T>, convert to array which implements IList<T>
            await _compressionService.CompressFilesAsync(filesToZip.ToArray(), configuration.DestinationDirectory, backupName, cancellationToken);
            _console.MarkupLine($"[green]Compressed all {filesToZip.Count} files![/]");
        }
        else
        {
            _console.MarkupLine("[yellow]No files to compress![/]");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 5. Save the File Index
        await _indexService.SaveBackupIndexAsync(backupIndex, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // 6. Trigger Recycle
        await _recycleService.RecycleBackupsAsync(cancellationToken);

        return Result.Success;
    }
}