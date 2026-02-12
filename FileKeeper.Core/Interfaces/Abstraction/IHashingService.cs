namespace FileKeeper.Core.Interfaces.Abstraction;

public interface IHashingService
{
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken);
    Task<string> ComputeHashFromStringAsync(string content, CancellationToken cancellationToken);
}