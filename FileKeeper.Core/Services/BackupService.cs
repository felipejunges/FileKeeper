using ErrorOr;
using FileKeeper.Core.Interfaces;
using FileKeeper.Core.Models;
using Spectre.Console;
using System.Text.Json;

namespace FileKeeper.Core.Services;

public class BackupService
{
    private readonly IAnsiConsole _console;
    private readonly Configuration _configuration;
    private readonly IFileSystem _fileSystem;
    private readonly IHashingService _hashingService;
    private readonly ICompressionService _compressionService;

    public BackupService(
        IAnsiConsole console,
        Configuration configuration,
        IFileSystem fileSystem,
        IHashingService hashingService,
        ICompressionService compressionService)
    {
        _console = console;
        _configuration = configuration;
        _fileSystem = fileSystem;
        _hashingService = hashingService;
        _compressionService = compressionService;
    }

    public async Task<ErrorOr<Success>> CreateBackupAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_configuration.DestinationDirectory) || _configuration.SourceDirectories.Count == 0)
        {
            _console.MarkupLine("[red]Configuration incomplete. Please set source and destination first.[/]");
            return Error.Failure(description: "Configuration incomplete. Please set source and destination first.");
        }

        _console.MarkupLine("[bold yellow]Starting Backup...[/]");

        var backupPath = Path.Combine(_configuration.DestinationDirectory, "backup.zip"); // TODO: think about the file extension (maybe just the file 'name' (without the extension))
        var backupName = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var tempDir = Path.Combine(_configuration.DestinationDirectory, "temp_files");

        // 1. Get The Backup Index and Previous File Index
        var backupIndexContent = await _compressionService.ReadFileContentAsync(backupPath, "index.json", cancellationToken);
        var backupIndex = backupIndexContent != null
            ? JsonSerializer.Deserialize<BackupIndex>(backupIndexContent)
            : new BackupIndex();

        var lastBackupMetadata = backupIndex?.Backups.OrderByDescending(b => b.CreatedAtUtc).FirstOrDefault();
        
        cancellationToken.ThrowIfCancellationRequested();

        // 2. Create the new Index Structure
        var newBackupMetadata = new BackupMetadata()
        {
            BackupName = backupName,
            CreatedAtUtc = DateTime.UtcNow
        };

        backupIndex?.Backups.Add(newBackupMetadata);

        // 3. Scan All Sources
        var filesToZip = new List<(string FullPath, string StoredPath)>();

        _fileSystem.CreateDirectory(tempDir);

        foreach (var sourceDir in _configuration.SourceDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var currentFiles = ScanSource(sourceDir);
            foreach (var file in currentFiles)
            {
                var existing = lastBackupMetadata?.Files.FirstOrDefault(f => f.StoredPath == file.StoredPath);

                bool isNew = true;
                if (existing != null)
                {
                    file.Hash = _hashingService.ComputeHash(Path.Combine(sourceDir, file.RelativePath));

                    if (existing.Hash == file.Hash)
                    {
                        isNew = false;
                        file.FoundInBackup = existing.FoundInBackup;
                    }
                }
                else
                {
                    file.Hash = _hashingService.ComputeHash(Path.Combine(sourceDir, file.RelativePath));
                }

                if (isNew)
                {
                    file.FoundInBackup = backupName;
                    filesToZip.Add((Path.Combine(sourceDir, file.RelativePath), file.StoredPath));
                }

                newBackupMetadata.AddFile(file);
            }
        }
        
        cancellationToken.ThrowIfCancellationRequested();

        // 4. Create Zip
        if (filesToZip.Any())
        {
            await _compressionService.CompressFilesAsync(filesToZip, backupPath, backupName, cancellationToken);
            _console.MarkupLine($"[green]Compressed all {filesToZip.Count} files![/]");
        }

        cancellationToken.ThrowIfCancellationRequested();
        
        // 5. Save the File Index
        var indexJson = JsonSerializer.Serialize(backupIndex, new JsonSerializerOptions { WriteIndented = true });
        await _compressionService.WriteFileContentAsync(backupPath, "index.json", indexJson, cancellationToken);

        // Cleanup
        if (_fileSystem.DirectoryExists(tempDir))
            _fileSystem.DeleteDirectory(tempDir, true);

        cancellationToken.ThrowIfCancellationRequested();
        
        // 6. Trigger Recycle
        // TODO: _recycleService.RecycleBackups(config.TargetDirectory, config.MaxBackupsToKeep);

        return Result.Success;
    }

    private List<FileMetadata> ScanSource(string sourceDir)
    {
        var result = new List<FileMetadata>();
        var sourceName =
            Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var files = _fileSystem.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var f in files)
        {
            var info = new FileInfo(f);
            var relPath = Path.GetRelativePath(sourceDir, f);
            result.Add(new FileMetadata
            {
                RelativePath = relPath,
                StoredPath = Path.Combine(sourceName, relPath),
                Size = info.Length, // TODO: esse cara dá problema em debug
                LastWriteTimeUtc = info.LastWriteTimeUtc
            });
        }

        return result;
    }
}