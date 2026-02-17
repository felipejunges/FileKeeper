using FileKeeper.Core.Models;

namespace FileKeeper.Tests.Builders;

public class BackupMetadataBuilder
{
    private readonly List<BackupMetadata> _backups = new List<BackupMetadata>();

    private BackupMetadataBuilder() { }

    public static BackupMetadataBuilder New() => new BackupMetadataBuilder();

    // Adds a ready BackupMetadata to the list
    public BackupMetadataBuilder AddBackup(BackupMetadata backup)
    {
        _backups.Add(backup);
        return this;
    }

    // Adds a new backup configured by the action
    public BackupMetadataBuilder AddBackup(Action<BackupMetadata> configure)
    {
        var backup = new BackupMetadata
        {
            BackupName = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            CreatedAtUtc = DateTime.UtcNow
        };

        configure(backup);
        _backups.Add(backup);
        return this;
    }

    public BackupMetadataBuilder AddBackup(DateTime createdAtUtc)
    {
        var backup = new BackupMetadata
        {
            BackupName = createdAtUtc.ToString("yyyyMMdd_HHmmss"),
            CreatedAtUtc = createdAtUtc
        };

        _backups.Add(backup);
        return this;
    }

    // Ensure there's a current backup to add files to
    private BackupMetadata EnsureCurrentBackup()
    {
        if (!_backups.Any())
        {
            AddBackup(DateTime.UtcNow);
        }

        return _backups.Last();
    }

    // Add file to the last backup
    public BackupMetadataBuilder AddFile(FileMetadata file)
    {
        var current = EnsureCurrentBackup();
        current.AddFile(file);
        return this;
    }

    public BackupMetadataBuilder AddFile(Action<FileMetadata> configure)
    {
        var file = new FileMetadata();
        configure(file);
        var current = EnsureCurrentBackup();
        current.AddFile(file);
        return this;
    }

    public BackupMetadataBuilder AddFiles(IEnumerable<FileMetadata> files)
    {
        var current = EnsureCurrentBackup();
        foreach (var f in files)
            current.AddFile(f);
        return this;
    }

    public BackupMetadataBuilder AddFile(string relativePath, string storedPath, long size, string hash, DateTime lastWriteUtc, string foundInBackup)
    {
        var file = new FileMetadata
        {
            RelativePath = relativePath,
            StoredPath = storedPath,
            Size = size,
            Hash = hash,
            LastWriteTimeUtc = lastWriteUtc,
            FoundInBackup = foundInBackup
        };

        var current = EnsureCurrentBackup();
        current.AddFile(file);
        return this;
    }

    // Return the built list of backups
    public List<BackupMetadata> Build() => _backups.ToList();
}