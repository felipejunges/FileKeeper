namespace FileKeeper.Core.Extensions;

public static class LongExtensions
{
    public static string ToHumanReadableSize(this long sizeInBytes)
    {
        if (sizeInBytes < 1024)
            return $"{sizeInBytes} B";

        double size = sizeInBytes;
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }
}