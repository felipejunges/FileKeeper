namespace FileKeeper.Core.Models.Entities;

public class FileVersion
{
    public required string Id { get; init; }
    public required string FileId { get; init; }
    public byte[]? Content { get; init; }
    public required string Hash { get; init; }
    public long Size { get; init; }
    public long VersionNumber { get; init; }
    public DateTime CreatedAt { get; init; }
}