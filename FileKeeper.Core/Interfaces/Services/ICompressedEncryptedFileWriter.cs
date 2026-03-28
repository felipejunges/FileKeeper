using ErrorOr;

namespace FileKeeper.Core.Interfaces.Services;

public interface ICompressedEncryptedFileWriter
{
    Task<ErrorOr<Success>> CompressFromStreamToFileAsync(string originFileName, string outputFilePath, CancellationToken token);

    Task<ErrorOr<Success>> DecompressAndDecryptFileAsync(
        string encryptedCompressedFilePath,
        string outputFilePath,
        CancellationToken token);
}