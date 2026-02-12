using System.Security.Cryptography;
using FileKeeper.Core.Interfaces.Abstraction;
using System.Text;

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

    public async Task<string> ComputeHashFromStringAsync(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }, cancellationToken);
    }
}