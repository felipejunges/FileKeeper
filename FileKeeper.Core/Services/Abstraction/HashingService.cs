using System.Security.Cryptography;
using FileKeeper.Core.Interfaces;

namespace FileKeeper.Core.Services.Abstraction;

public class HashingService : IHashingService
{
    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using (var sha256 = SHA256.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}