namespace FileKeeper.Core;

public static class Formatting
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {Units[unit]}";
    }

    /// <summary>Formats snapshot ids compactly, e.g. [1,2,3,5] -> "#1–#3, #5".</summary>
    public static string SnapshotRanges(IEnumerable<int> ids)
    {
        var sorted = ids.Distinct().Order().ToList();
        var parts = new List<string>();
        for (var i = 0; i < sorted.Count;)
        {
            var start = sorted[i];
            var end = start;
            while (i + 1 < sorted.Count && sorted[i + 1] == end + 1)
                end = sorted[++i];
            i++;
            parts.Add(start == end ? $"#{start}" : $"#{start}–#{end}");
        }

        return string.Join(", ", parts);
    }
}
