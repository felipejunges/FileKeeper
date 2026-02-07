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

    public void CreateBackup()
    {
        if (string.IsNullOrEmpty(_configuration.DestinationDirectory) || _configuration.SourceDirectories.Count == 0)
        {
            _console.MarkupLine("[red]Configuration incomplete. Please set source and destination first.[/]");
            return;
        }
        
        _console.MarkupLine("[bold yellow]Starting Backup...[/]");
        
        var backupName = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(_configuration.DestinationDirectory, backupName);
        var tempDir = Path.Combine(backupPath, "temp_files");

        // 1. Get Previous Backup Index
        var lastBackup = GetLastBackup(_configuration.DestinationDirectory);
        FileIndex? lastIndex = null;
        if (lastBackup != null)
        {
            var lastIndexPath = Path.Combine(lastBackup, "index.json");
            if (File.Exists(lastIndexPath))
            {
                var json = File.ReadAllText(lastIndexPath);
                lastIndex = JsonSerializer.Deserialize<FileIndex>(json);
            }
        }

        // 2. Scan All Sources
        var newIndex = new FileIndex()
        {
            BackupName = backupName,
            CreatedAtUtc = DateTime.UtcNow,
            Files = new List<FileMetadata>()
        };

        var filesToZip = new List<(string FullPath, string StoredPath)>();

        _fileSystem.CreateDirectory(tempDir);

        foreach (var sourceDir in _configuration.SourceDirectories)
        {
            var currentFiles = ScanSource(sourceDir);
            foreach (var file in currentFiles)
            {
                var existing = lastIndex?.Files.FirstOrDefault(f => f.StoredPath == file.StoredPath);

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

                newIndex.Files.Add(file);
            }
        }

        // 3. Create Zip
        if (filesToZip.Any())
        {
            _compressionService.CompressFiles(filesToZip, backupPath, "files");
            _console.MarkupLine($"[green]Compressed all {filesToZip.Count} files![/]");
        }

        // 4. Save Index
        var indexJson = JsonSerializer.Serialize(newIndex, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.WriteAllText(Path.Combine(backupPath, "index.json"), indexJson);

        // Cleanup
        if (_fileSystem.DirectoryExists(tempDir))
            _fileSystem.DeleteDirectory(tempDir, true);

        // 5. Trigger Recycle
        // TODO: _recycleService.RecycleBackups(config.TargetDirectory, config.MaxBackupsToKeep);
    }

    private string? GetLastBackup(string targetDir)
    {
        if (!_fileSystem.DirectoryExists(targetDir)) return null;
        var dirs = _fileSystem.GetDirectories(targetDir)
            .OrderByDescending(d => _fileSystem.GetDirectoryCreationTimeUtc(d)).ToList();
        
        return dirs.FirstOrDefault();
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
                Size = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc
            });
        }

        return result;
    }
}