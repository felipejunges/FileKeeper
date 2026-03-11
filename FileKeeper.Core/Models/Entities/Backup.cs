using FileKeeper.Core.Extensions;

namespace FileKeeper.Core.Models.Entities;

public class Backup
{
    public long Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int CreatedFiles { get; private set; }
    public int UpdatedFiles { get; private set; }
    public int DeletedFiles { get; private set; }
    public long TotalSize { get; private set; }
    
    public string TotalSizeFormatted => TotalSize.ToHumanReadableSize();

    private Backup()
    {
    }

    public Backup(long id, DateTime createdAt, int createdFiles, int updatedFiles, int deletedFiles, long totalSize)
    {
        Id = id;
        CreatedAt = createdAt;
        CreatedFiles = createdFiles;
        UpdatedFiles = updatedFiles;
        DeletedFiles = deletedFiles;
        TotalSize = totalSize;
    }

    public static Backup CreateNew()
    {
        return new Backup
        {
            Id = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedFiles = 0,
            UpdatedFiles = 0,
            DeletedFiles = 0,
            TotalSize = 0
        };
    }

    public void UpdateId(long id)
    {
        if (Id != 0) return;

        Id = id;
    }

    public void IncrementCreatedFiles(int count = 1)
    {
        CreatedFiles += count;
    }

    public void IncrementUpdatedFiles(int count = 1)
    {
        UpdatedFiles += count;
    }

    public void IncrementDeletedFiles(int count = 1)
    {
        DeletedFiles += count;
    }

    public void IncrementTotalSize(long size)
    {
        TotalSize += size;
    }

    public void UpdateTotalsAndSize(int createdFiles, int updatedFiles, int deletedFiles, long totalSize)
    {
        CreatedFiles = createdFiles;
        UpdatedFiles = updatedFiles;
        DeletedFiles = deletedFiles;
        TotalSize = totalSize;
    }
}