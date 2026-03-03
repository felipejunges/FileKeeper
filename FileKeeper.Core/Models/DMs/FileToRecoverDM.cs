using FileKeeper.Core.Helpers;

namespace FileKeeper.Core.Models.DMs;

public class FileToRecoverDM
{
    public long Id { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long BackupId { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; } = string.Empty;
    public byte[]? Content { get; set; }
    
    public byte[] DecompressedContent => Content == null ? [] : CompressionHelper.Decompress(Content);
}