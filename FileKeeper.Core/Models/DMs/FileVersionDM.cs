namespace FileKeeper.Core.Models.DMs;

public class FileVersionDM
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required string CurrentHash { get; init; }
    public long CurrentVersionNumber { get; init; }
}