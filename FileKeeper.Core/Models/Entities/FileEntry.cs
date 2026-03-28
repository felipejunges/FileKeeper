namespace FileKeeper.Core.Models.Entities;

public class FileEntry
{
    public Guid Id { get; private set; }

    public string RelativePath { get; private set; } = string.Empty;

    public string StoredPath { get; private set; } = string.Empty;

    public string Hash { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public DateTime LastModified { get; private set; }

    public string FoundInSnapshot { get; private set; } = string.Empty;

    public FileEntry()
    {
    }

    public FileEntry(Guid id, string relativePath, string storedPath, string hash, long size, DateTime lastModified, string foundInSnapshot)
    {
        Id = id;
        RelativePath = relativePath;
        StoredPath = storedPath;
        Hash = hash;
        Size = size;
        LastModified = lastModified;
        FoundInSnapshot = foundInSnapshot;
    }

    public static FileEntry Create(string relativePath, string storedPath, string hash, long size, DateTime lastModified, string snapshotId)
    {
        return new FileEntry(
            Guid.CreateVersion7(),
            relativePath,
            storedPath,
            hash,
            size,
            lastModified,
            snapshotId);
    }
}