using System.IO.Compression;

namespace FileKeeper.Core.Helpers;

/// <summary>
/// Provides compression and decompression utilities for byte arrays.
/// Uses GZip compression for efficient storage.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Compresses data using GZip compression.
    /// </summary>
    /// <param name="data">The uncompressed data to compress.</param>
    /// <returns>Compressed byte array.</returns>
    public static byte[] Compress(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
            return Array.Empty<byte>();

        using (var outputStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, false))
            {
                gzipStream.Write(data, 0, data.Length);
                gzipStream.Flush();
            }

            return outputStream.ToArray();
        }
    }

    /// <summary>
    /// Decompresses GZip-compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data to decompress.</param>
    /// <returns>Decompressed byte array.</returns>
    public static byte[] Decompress(byte[] compressedData)
    {
        ArgumentNullException.ThrowIfNull(compressedData);

        if (compressedData.Length == 0)
            return Array.Empty<byte>();

        using (var inputStream = new MemoryStream(compressedData))
        using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, false))
        using (var outputStream = new MemoryStream())
        {
            gzipStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }

    /// <summary>
    /// Asynchronously compresses data using GZip compression.
    /// </summary>
    /// <param name="data">The uncompressed data to compress.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Compressed byte array.</returns>
    public static async Task<byte[]> CompressAsync(byte[] data, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
            return Array.Empty<byte>();

        using (var outputStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, false))
            {
                await gzipStream.WriteAsync(data, 0, data.Length, token);
                await gzipStream.FlushAsync(token);
            }

            return outputStream.ToArray();
        }
    }

    /// <summary>
    /// Asynchronously decompresses GZip-compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data to decompress.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Decompressed byte array.</returns>
    public static async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(compressedData);

        if (compressedData.Length == 0)
            return Array.Empty<byte>();

        using (var inputStream = new MemoryStream(compressedData))
        using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, false))
        using (var outputStream = new MemoryStream())
        {
            await gzipStream.CopyToAsync(outputStream, token);
            return outputStream.ToArray();
        }
    }

    /// <summary>
    /// Calculates the compression ratio (compressed size / original size).
    /// </summary>
    /// <param name="originalSize">Size of original data.</param>
    /// <param name="compressedSize">Size of compressed data.</param>
    /// <returns>Compression ratio as a percentage.</returns>
    public static double GetCompressionRatio(int originalSize, int compressedSize)
    {
        if (originalSize == 0)
            return 0;

        return (1.0 - (double)compressedSize / originalSize) * 100;
    }
}