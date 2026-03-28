using ErrorOr;
using FileKeeper.Core.Interfaces.Services;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.IO.Compression;
using System.Security.Cryptography;

namespace FileKeeper.Core.Services;

public class CompressedEncryptedFileWriter : ICompressedEncryptedFileWriter
{
    private readonly string _defaultPassPhrase = "phelipe_123";
    private readonly int _saltSizeBytes = 32; // 256 bits
    private readonly int _iterations = 100_000;
    private readonly int _keySizeBytes = 32; // 256 bits

    public async Task<ErrorOr<Success>> CompressFromStreamToFileAsync(Stream sourceStream, string fullFileName, CancellationToken token)
    {
        try
        {
            await using var fileStream = File.Create(fullFileName);

            // Generate salt and IV
            var salt = new byte[_saltSizeBytes];
            RandomNumberGenerator.Fill(salt);

            using var aes = Aes.Create();
            aes.GenerateIV();

            // Write salt and IV to file (needed for decryption)
            await fileStream.WriteAsync(salt, 0, salt.Length, token);
            await fileStream.WriteAsync(aes.IV, 0, aes.IV.Length, token);

            // Derive key using the salt
            var encryptionKey = DeriveKeyFromPassphraseWithSalt(_defaultPassPhrase, salt);
            aes.Key = encryptionKey;

            // Encryption -> Compression chain
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            await using var gzipStream = new GZipStream(cryptoStream, CompressionMode.Compress, false);

            sourceStream.Position = 0;
            await sourceStream.CopyToAsync(gzipStream, 81920, token);
            await gzipStream.FlushAsync(token);
            await cryptoStream.FlushAsync(token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure($"Failed to encrypt and compress file: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> DecompressAndDecryptFileAsync(
        string encryptedCompressedFilePath,
        string outputFilePath,
        CancellationToken token)
    {
        try
        {
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await using var fileStream = File.OpenRead(encryptedCompressedFilePath);

            // Read salt and IV from the beginning of the file
            byte[] salt = new byte[_saltSizeBytes];
            int saltBytesRead = await fileStream.ReadAsync(salt, 0, salt.Length, token);
            if (saltBytesRead != salt.Length)
                return Error.Failure("Failed to read salt from encrypted file");

            byte[] iv = new byte[16];
            int ivBytesRead = await fileStream.ReadAsync(iv, 0, iv.Length, token);
            if (ivBytesRead != iv.Length)
                return Error.Failure("Failed to read IV from encrypted file");

            // Derive the same key using the stored salt
            var encryptionKey = DeriveKeyFromPassphraseWithSalt(_defaultPassPhrase, salt);

            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.IV = iv;

            // Decryption -> Decompression chain
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            await using var gzipStream = new GZipStream(cryptoStream, CompressionMode.Decompress);
            await using var outputStream = File.Create(outputFilePath);

            await gzipStream.CopyToAsync(outputStream, 81920, token);
            await outputStream.FlushAsync(token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure($"Failed to decompress and decrypt file: {ex.Message}");
        }
    }

    private byte[] DeriveKeyFromPassphraseWithSalt(string passPhrase, byte[] salt)
    {
        return KeyDerivation.Pbkdf2(
            password: passPhrase,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: _iterations,
            numBytesRequested: _keySizeBytes
        );
    }
}