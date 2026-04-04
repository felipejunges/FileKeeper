using ErrorOr;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.Wrappers;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Security.Cryptography;

namespace FileKeeper.Core.Services;

public class CompressedEncryptedFileWriter : ICompressedEncryptedFileWriter
{
    private const string DefaultPassPhrase = "phelipe_123";
    private const int SaltSizeBytes = 32; // 256 bits
    private const int AesIvSizeBytes = 16; // AES block size is 128 bits
    private const int Iterations = 100_000;
    private const int KeySizeBytes = 32; // 256 bits
    private readonly ILogger<CompressedEncryptedFileWriter> _logger;
    
    private readonly IFileWrapper _fileWrapper;

    public CompressedEncryptedFileWriter(
        IFileWrapper fileWrapper,
        ILogger<CompressedEncryptedFileWriter> logger)
    {
        _fileWrapper = fileWrapper;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> CompressFromStreamToFileAsync(string originFileName, string outputFilePath, CancellationToken token)
    {
        try
        {
            await using var sourceStream = _fileWrapper.OpenRead(originFileName);
            await using var fileStream = _fileWrapper.Create(outputFilePath);

            // Generate salt and IV
            var salt = new byte[SaltSizeBytes];
            RandomNumberGenerator.Fill(salt);

            using var aes = Aes.Create();
            aes.GenerateIV();

            // Write salt and IV to file (needed for decryption)
            await fileStream.WriteAsync(salt, 0, salt.Length, token);
            await fileStream.WriteAsync(aes.IV, 0, aes.IV.Length, token);

            // Derive key using the salt
            var encryptionKey = DeriveKeyFromPassphraseWithSalt(DefaultPassPhrase, salt);
            aes.Key = encryptionKey;

            // Encryption -> Compression chain
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            await using var gzipStream = new GZipStream(cryptoStream, CompressionMode.Compress, false);

            await sourceStream.CopyToAsync(gzipStream, 81920, token);
            await gzipStream.FlushAsync(token);
            await cryptoStream.FlushAsync(token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error compressing and encrypting file {FileName} to {OutputFilePath}",
                originFileName,
                outputFilePath);
            
            return Error.Failure(description: $"Failed to encrypt and compress file: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Success>> DecompressAndDecryptFileAsync(string encryptedCompressedFilePath, string outputFilePath, CancellationToken token)
    {
        try
        {
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await using var fileStream = _fileWrapper.OpenRead(encryptedCompressedFilePath);

            // Read salt and IV from the beginning of the file
            byte[] salt = new byte[SaltSizeBytes];
            int saltBytesRead = await fileStream.ReadAsync(salt, 0, salt.Length, token);
            if (saltBytesRead != salt.Length)
                return Error.Failure(description: "Failed to read salt from encrypted file");

            byte[] iv = new byte[AesIvSizeBytes];
            int ivBytesRead = await fileStream.ReadAsync(iv, 0, iv.Length, token);
            if (ivBytesRead != iv.Length)
                return Error.Failure(description: "Failed to read IV from encrypted file");

            // Derive the same key using the stored salt
            var encryptionKey = DeriveKeyFromPassphraseWithSalt(DefaultPassPhrase, salt);

            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.IV = iv;

            // Decryption -> Decompression chain
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            await using var gzipStream = new GZipStream(cryptoStream, CompressionMode.Decompress);
            await using var outputStream = _fileWrapper.Create(outputFilePath);

            await gzipStream.CopyToAsync(outputStream, 81920, token);
            await outputStream.FlushAsync(token);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Error decompressing and decrypting file {EncryptedCompressedFilePath} to {OutputFilePath}",
                encryptedCompressedFilePath,
                outputFilePath);
            
            return Error.Failure(description: $"Failed to decompress and decrypt file: {ex.Message}");
        }
    }

    private byte[] DeriveKeyFromPassphraseWithSalt(string passPhrase, byte[] salt)
    {
        return KeyDerivation.Pbkdf2(
            password: passPhrase,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: KeySizeBytes
        );
    }
}