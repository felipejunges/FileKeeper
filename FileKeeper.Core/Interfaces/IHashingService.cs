namespace FileKeeper.Core.Interfaces;

public interface IHashingService
{
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken);
}