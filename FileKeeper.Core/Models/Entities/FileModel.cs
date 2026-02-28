namespace FileKeeper.Core.Models.Entities;

public class FileModel
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
    public bool IsDeleted { get; init; }
    public long? DeletedVersionNumber { get; init; }
}