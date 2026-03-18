namespace FileKeeper.Core.Models.Entities;

public class FileModel
{
    public long Id { get; private set; }
    public string BackupPath { get; private set; } = null!;
    public string RelativePath { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public long? DeletedAt { get; private set; }

    private FileModel()
    {
    }

    public static FileModel CreateNew(string backupPath, string relativePath, string fileName)
    {
        return new FileModel
        {
            Id = 0,
            BackupPath = backupPath,
            RelativePath = relativePath,
            FileName = fileName,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    public void UpdateId(long id)
    {
        if (Id != 0) return;

        Id = id;
    }

    public void UpdateDeletedAt(long deletedAt)
    {
        IsDeleted = true;
        DeletedAt = deletedAt;
    }
}