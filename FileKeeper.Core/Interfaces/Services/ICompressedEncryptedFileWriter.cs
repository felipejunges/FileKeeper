using ErrorOr;

namespace FileKeeper.Core.Interfaces.Services;

public interface ICompressedEncryptedFileWriter
{
    Task<ErrorOr<Success>> CompressFromStreamToFileAsync(Stream sourceStream, string fullFileName, CancellationToken token);

    Task<ErrorOr<Success>> DecompressAndDecryptFileAsync(
        string encryptedCompressedFilePath,
        string outputFilePath,
        CancellationToken token);
}