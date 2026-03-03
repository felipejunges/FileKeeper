using FileKeeper.Core.Helpers;

namespace FileKeeper.Core.Models.Entities;

public class FileVersion
{
    public  long Id { get; private set; }
    public long FileId { get; private set; }
    public long BackupId { get; private set; }
    public long Size { get; private set; }
    public string Hash { get; private set; } = null!;
    public byte[]? Content { get; private set; }
    
    public byte[] CompressedContent => Content == null ? [] : CompressionHelper.Compress(Content);

    private FileVersion()
    {
    }
    
    public static FileVersion CreateNew(long fileId, long backupId, long size, string hash, byte[] content)
    {
        return new FileVersion
        {
            Id = 0,
            FileId = fileId,
            BackupId = backupId,
            Size = size,
            Hash = hash,
            Content = content
        };
    }
    
    public void UpdateId(long id)
    {
        if (Id != 0) return;
        
        Id = id;
    }
}