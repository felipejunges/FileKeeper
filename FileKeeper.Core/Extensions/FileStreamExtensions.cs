namespace FileKeeper.Core.Extensions;

public static class FileStreamExtensions
{
    public static async Task<byte[]> ReadAllBytesAsync(this FileStream fileStream, CancellationToken cancellationToken = default)
    {
        fileStream.Position = 0;
        var buffer = new byte[fileStream.Length];
        await fileStream.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }
}