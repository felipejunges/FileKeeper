namespace FileKeeper.Core.Services;

/// <summary>A file found in a source folder.</summary>
public sealed record ScannedFile(string FullPath, string ArchivePath, long Size, DateTime LastWriteTimeUtc);

/// <summary>
/// Walks the source folders, skipping ignored folders, symbolic links and excluded files.
/// </summary>
public sealed class FileScanner
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly List<string> _ignoredPaths = [];
    private readonly HashSet<string> _ignoredNames;
    private readonly HashSet<string> _excludedFiles;

    public FileScanner(IEnumerable<string> ignoredFolders, IEnumerable<string>? excludedFiles = null)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _ignoredNames = new HashSet<string>(comparer);
        _excludedFiles = new HashSet<string>(comparer);

        foreach (var entry in ignoredFolders.Select(e => e.Trim()).Where(e => e.Length > 0))
        {
            if (Path.IsPathFullyQualified(entry))
                _ignoredPaths.Add(Normalize(entry));
            else
                _ignoredNames.Add(entry.Trim('/', '\\'));
        }

        foreach (var file in excludedFiles ?? [])
            _excludedFiles.Add(Path.GetFullPath(file));
    }

    public List<string> Errors { get; } = [];

    /// <summary>
    /// Stable, unique archive prefix for a source folder: its full path without the drive colon,
    /// e.g. "/home/me/Docs" -> "home/me/Docs" and "C:\Users\me" -> "C/Users/me".
    /// </summary>
    public static string SourceKey(string sourceFolder)
    {
        var key = Normalize(sourceFolder).Replace('\\', '/').Replace(":", string.Empty).Trim('/');
        return key.Length == 0 ? "root" : key;
    }

    public bool IsIgnored(string directoryFullPath)
    {
        var normalized = Normalize(directoryFullPath);
        if (_ignoredNames.Contains(Path.GetFileName(normalized)))
            return true;

        return _ignoredPaths.Any(ignored =>
            normalized.Equals(ignored, PathComparison) ||
            normalized.StartsWith(ignored + Path.DirectorySeparatorChar, PathComparison));
    }

    public IEnumerable<ScannedFile> Scan(IEnumerable<string> sourceFolders, CancellationToken ct = default)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sourceFolders)
        {
            var root = Normalize(source);
            var key = SourceKey(root);
            if (!seenKeys.Add(key))
                continue;

            if (!Directory.Exists(root))
            {
                Errors.Add($"Source folder not found: {root}");
                continue;
            }

            if (IsIgnored(root))
                continue;

            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(root));

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                FileSystemInfo[] children;
                try
                {
                    children = dir.GetFileSystemInfos();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    Errors.Add($"Cannot read folder {dir.FullName}: {ex.Message}");
                    continue;
                }

                // Sorted for deterministic snapshots.
                foreach (var child in children.OrderBy(c => c.Name, StringComparer.Ordinal))
                {
                    ScannedFile? scanned;
                    try
                    {
                        scanned = Inspect(child, root, key, pending);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        // Typically a file deleted during the scan, or (on Linux) a name that is not
                        // valid UTF-8: .NET decodes it lossily ('�') and can no longer open it.
                        var hint = child.Name.Contains('�')
                            ? " (the name has invalid characters; rename the file to back it up)"
                            : string.Empty;
                        Errors.Add($"Cannot read {child.FullName}: {ex.Message}{hint}");
                        continue;
                    }

                    if (scanned is not null)
                        yield return scanned;
                }
            }
        }
    }

    /// <summary>Queues a sub folder or returns the file entry; null when the child is skipped.</summary>
    private ScannedFile? Inspect(FileSystemInfo child, string root, string key, Stack<DirectoryInfo> pending)
    {
        if (child.LinkTarget is not null)
            return null;

        if (child is DirectoryInfo subDir)
        {
            if (!IsIgnored(subDir.FullName))
                pending.Push(subDir);
            return null;
        }

        if (child is not FileInfo file || _excludedFiles.Contains(file.FullName))
            return null;

        var relative = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
        return new ScannedFile(file.FullName, $"{key}/{relative}", file.Length, file.LastWriteTimeUtc);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
