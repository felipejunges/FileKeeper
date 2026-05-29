using ErrorOr;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DTOs;
using FileKeeper.Core.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace FileKeeper.Core.Repositories;

public class ZipFileStoreRepository : IFileStoreRepository
{
    private readonly IOptionsMonitor<UserSettingsOptions> _userSettingsOptions;
    private readonly ILogger<ZipFileStoreRepository> _logger;

    private const int BufferSize = 81920;
    private string BackupZipPath() => Path.Combine(_userSettingsOptions.CurrentValue.StorageDirectory, "store.zip");

    public ZipFileStoreRepository(
        IOptionsMonitor<UserSettingsOptions> userSettingsOptions,
        ILogger<ZipFileStoreRepository> logger)
    {
        _userSettingsOptions = userSettingsOptions;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> AddFileAsync(FileToSave file, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("Adding file {File} to the  repository.", file.FullPath);
            
            await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

            await archive.CreateEntryFromFileAsync(
                file.FullPath,
                file.StoredPath,
                token);
            
            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file {SourceFilePath} to the repository.", file.FullPath);
            
            return Error.Failure(
                code: $"{nameof(ZipFileStoreRepository)}.{nameof(AddFileAsync)}",
                description: $"Error adding file {file.FullPath} to the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> AddFilesAsync(IEnumerable<FileToSave> files, IProgress<BackupProgress>? progress, CancellationToken token)
    {
        var currentFileName = string.Empty;
        
        try
        {
            var filesList = files.ToList();
            
            _logger.LogInformation("Adding batch of {Count} files to the  repository.", filesList.Count);
            
            await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

            foreach (var file in filesList)
            {
                token.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Adding file {File} to the  repository.", file.FullPath);
                currentFileName = file.FullPath;

                progress?.Report(CreatePercentageBackupProgress(filesList, file));

                await archive.CreateEntryFromFileAsync(
                    file.FullPath,
                    file.StoredPath,
                    token);
            }

            progress?.Report(new SimpleBackupProgress()
            {
                Process = "Closing file"
            });
            
            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file {File} from the batch list to the repository.", currentFileName);
            
            return Error.Failure(
                code: $"{nameof(ZipFileStoreRepository)}.{nameof(AddFilesAsync)}",
                description: $"Error adding file {currentFileName} to the repository: {ex.Message}");
        }
    }

    public async Task AddStreamAsync(Stream sourceStream, string entryPath, CancellationToken token)
    {
        if (!sourceStream.CanRead)
            throw new ArgumentException("Source stream must be readable.", nameof(sourceStream));

        if (sourceStream.CanSeek)
            sourceStream.Position = 0;

        await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

        var existing = archive.GetEntry(entryPath);
        existing?.Delete();

        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);

        await using var destination = await entry.OpenAsync(token);

        await sourceStream.CopyToAsync(destination, BufferSize, token);
    }

    public async Task<Stream> GetFileContentStreamAsync(string entryPath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenReadAsync(BackupZipPath(), token);

        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException("The requested archive entry was not found.", entryPath);

        var output = new MemoryStream();

        // Open entry stream and copy into memory so we can dispose the archive safely
        await using (var source = await entry.OpenAsync(token))
        {
            await source.CopyToAsync(output, BufferSize, token);
        }

        output.Position = 0;
        return output;
    }

    public async Task ExtractFileAsync(FileToRestore file, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenReadAsync(BackupZipPath(), token);

        var entry = archive.GetEntry(file.StoredPath);
        if (entry == null)
            throw new FileNotFoundException("The requested archive entry was not found.", file.StoredPath);

        await entry.ExtractToFileAsync(file.FullPath, token);
    }

    public async Task ExtractFilesAsync(IEnumerable<FileToRestore> files, IProgress<BackupProgress>? progress, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenReadAsync(BackupZipPath(), token);

        var filesList = files.ToList();

        foreach (var file in filesList)
        {
            token.ThrowIfCancellationRequested();

            progress?.Report(new PercentageBackupProgress()
            {
                CurrentFileIndex = filesList.IndexOf(file) + 1,
                TotalFiles = filesList.Count,
                CurrentFileName = file.StoredPath,
                CurrentFolder = file.FullPath,
                Process = "Extracting"
            });

            var entry = archive.GetEntry(file.StoredPath);
            if (entry == null)
                throw new FileNotFoundException("The requested archive entry was not found.", file.StoredPath);

            await entry.ExtractToFileAsync(file.FullPath, token);
        }
    }

    public async Task ExtractAllAsync(string destinationDirectoryPath, CancellationToken token)
    {
        await using var archive = await ZipFile.OpenReadAsync(BackupZipPath(), token);

        await archive.ExtractToDirectoryAsync(destinationDirectoryPath, true, cancellationToken: token);
    }

    public async Task<ErrorOr<Deleted>> DeleteFileAsync(FileToDelete file, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("Deleteing file {File} to the  repository.", file.StoredPath);
            
            await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

            var entry = archive.GetEntry(file.StoredPath);
            if (entry == null)
                throw new FileNotFoundException("The requested archive entry was not found.", file.StoredPath);

            entry.Delete();

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {StoredPath} in the repository.", file.StoredPath);
            
            return Error.Failure(
                code: $"{nameof(ZipFileStoreRepository)}.{nameof(DeleteFileAsync)}",
                description: $"Error deleting file {file.StoredPath} from the repository: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Deleted>> DeleteFilesAsync(IEnumerable<FileToDelete> files, CancellationToken token)
    {
        var currentFileName = string.Empty;
        
        try
        {
            var filesList = files.ToList();
            
            _logger.LogInformation("Deleting batch of {Count} files to the  repository.", filesList.Count);
            
            await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

            foreach (var file in filesList)
            {
                currentFileName = file.StoredPath;
                
                var entry = archive.GetEntry(file.StoredPath);
                if (entry == null)
                    throw new FileNotFoundException("The requested archive entry was not found.", file.StoredPath);

                entry.Delete();
            }

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {File} from the batch list to the repository.", currentFileName);
            
            return Error.Failure(
                code: $"{nameof(ZipFileStoreRepository)}.{nameof(DeleteFilesAsync)}",
                description: $"Error deleting files from the repository: {ex.Message}");
        }
    }

    public async Task RemoveEmptyDirectoriesAsync(CancellationToken token)
    {
        await using var archive = await ZipFile.OpenAsync(BackupZipPath(), ZipArchiveMode.Update, token);

        var emptyDirectories = archive.Entries
            .Where(entry => (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) 
                            && entry.Length == 0)
            .ToList();

        foreach (var directory in emptyDirectories)
        {
            token.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Removing empty directory {Directory}", directory.FullName);
            
            directory.Delete();
        }
    }
    
    private static PercentageBackupProgress CreatePercentageBackupProgress(List<FileToSave> filesList, FileToSave file)
    {
        return new PercentageBackupProgress()
        {
            CurrentFileIndex = filesList.IndexOf(file) + 1,
            TotalFiles = filesList.Count,
            CurrentFileName = file.RelativePath,
            CurrentFolder = file.FullPath,
            Process = "Zipping"
        };
    }
}