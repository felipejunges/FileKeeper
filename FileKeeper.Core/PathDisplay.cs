using FileKeeper.Core.Services;

namespace FileKeeper.Core;

/// <summary>
/// Shortens archive paths for display by replacing the source key ("home/me/Documents")
/// with the source folder name ("Documents"), unless two sources share that name.
/// </summary>
public sealed class PathDisplay
{
    private readonly List<(string Key, string Name)> _prefixes;

    public PathDisplay(IEnumerable<string> sourceFolders)
    {
        var keys = sourceFolders.Select(FileScanner.SourceKey).Distinct().ToList();
        _prefixes = keys
            .Select(key => (Key: key, Name: key[(key.LastIndexOf('/') + 1)..]))
            .GroupBy(p => p.Name)
            .SelectMany(g => g.Count() == 1 ? g : g.Select(p => (p.Key, Name: p.Key)))
            .OrderByDescending(p => p.Key.Length)
            .ToList();
    }

    public string Shorten(string archivePath)
    {
        foreach (var (key, name) in _prefixes)
        {
            if (archivePath.Length > key.Length && archivePath[key.Length] == '/' &&
                archivePath.StartsWith(key, StringComparison.Ordinal))
                return name + archivePath[key.Length..];
        }

        return archivePath;
    }
}
