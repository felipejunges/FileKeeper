using ErrorOr;
using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Interfaces.Abstraction;
using FileKeeper.Core.Interfaces.Abstraction.Info;
using FileKeeper.Core.Models;
using FileKeeper.Core.Utils;
using Spectre.Console;

namespace FileKeeper.Core.Services;

public class BackupService
{
    private readonly IAnsiConsole _console;
    private readonly IConfigurationService _configurationService;
    private readonly IFileSystem _fileSystem;
    private readonly ICompressionService _compressionService;
    private readonly IRecycleService _recycleService;
    private readonly IFileInfoBuilder _fileInfoBuilder;
    private readonly IIndexService _indexService;

    public BackupService(
        IAnsiConsole console,
        IConfigurationService configurationService,
        IFileSystem fileSystem,
        ICompressionService compressionService,
        IRecycleService recycleService,
        IFileInfoBuilder fileInfoBuilder,
        IIndexService indexService)
    {
        _console = console;
        _configurationService = configurationService;
        _fileSystem = fileSystem;
        _compressionService = compressionService;
        _recycleService = recycleService;
        _fileInfoBuilder = fileInfoBuilder;
        _indexService = indexService;
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

        foreach (var sourceDir in configuration.SourceDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentFiles = ScanSource(sourceDir, configuration.ExcludePatterns);

            await Parallel.ForEachAsync(currentFiles, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, async (file, ct) =>
            {
                var existing = lastBackupMetadata?.Files.FirstOrDefault(f => f.IsSameFile(file));

                if (existing != null)
                {
                    file.Hash = await _fileSystem.ComputeHashAsync(Path.Combine(sourceDir, file.RelativePath), ct);

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
                    file.Hash = await _fileSystem.ComputeHashAsync(Path.Combine(sourceDir, file.RelativePath), ct);
                    file.FoundInBackup = backupName;
                    filesToZip.Add((Path.Combine(sourceDir, file.RelativePath), file.StoredPath));
                }

                newBackupMetadata.AddFile(file);
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

    private List<FileMetadata> ScanSource(string sourceDir, IEnumerable<string> excludePatterns)
    {
        var result = new List<FileMetadata>();

        // Materialize excludePatterns to avoid multiple enumeration
        var excludeList = excludePatterns.ToList();

        var sourceDirBase64 = EncodingUtils.ToBase64(sourceDir);
        var files = _fileSystem.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var f in files)
        {
            var relPath = Path.GetRelativePath(sourceDir, f);

            // TODO: create unit test for this
            if (excludeList.Any(p => relPath.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var info = _fileInfoBuilder.Build(f);

            result.Add(new FileMetadata
            {
                RelativePath = relPath,
                StoredPath = Path.Combine(sourceDirBase64, relPath),
                Size = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc
            });
        }

        return result;
    }
}