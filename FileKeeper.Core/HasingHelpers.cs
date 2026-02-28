using System.Security.Cryptography;
using System.Text;

namespace FileKeeper.Core;

public static class HasingHelpers
{
    public static async Task<string> ComputeHashFromStringAsync(string content, CancellationToken cancellationToken)
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

    public static async Task<string> ComputeHashFromStreamAsync(FileStream stream, CancellationToken cancellationToken)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}