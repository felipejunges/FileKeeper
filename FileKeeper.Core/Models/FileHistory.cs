namespace FileKeeper.Core.Models;

/// <summary>A distinct stored content of a file and the snapshots that contain it.</summary>
public sealed record FileVersion(
    int StoredIn,
    string Sha256,
    long Size,
    DateTime LastWriteTimeUtc,
    IReadOnlyList<int> SnapshotIds);

/// <summary>Every version of one file path across all snapshots.</summary>
public sealed record FileHistory(string Path, IReadOnlyList<FileVersion> Versions, bool ExistsInLatest)
{
    public FileVersion LatestVersion => Versions[^1];

    public IEnumerable<int> SnapshotIds => Versions.SelectMany(v => v.SnapshotIds);

    /// <summary>Builds the history of every path that appears in at least one snapshot.</summary>
    public static IReadOnlyList<FileHistory> Build(IReadOnlyList<SnapshotManifest> snapshots)
    {
        var ordered = snapshots.OrderBy(s => s.Id).ToList();
        // path -> (storedIn -> version data), kept in snapshot order
        var byPath = new Dictionary<string, List<(FileRecord Record, List<int> Ids)>>(StringComparer.Ordinal);

        foreach (var snapshot in ordered)
        {
            foreach (var file in snapshot.Files)
            {
                if (!byPath.TryGetValue(file.Path, out var versions))
                    byPath[file.Path] = versions = [];

                var index = versions.FindIndex(v => v.Record.StoredIn == file.StoredIn);
                if (index < 0)
                {
                    versions.Add((file, [snapshot.Id]));
                }
                else
                {
                    // Same content; keep the most recent metadata (e.g. touched timestamp).
                    versions[index] = (file, versions[index].Ids);
                    versions[index].Ids.Add(snapshot.Id);
                }
            }
        }

        var latest = ordered.LastOrDefault();
        var latestPaths = latest is null
            ? new HashSet<string>()
            : latest.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        return byPath
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new FileHistory(
                kv.Key,
                kv.Value.Select(v => new FileVersion(
                    v.Record.StoredIn, v.Record.Sha256, v.Record.Size, v.Record.LastWriteTimeUtc, v.Ids)).ToList(),
                latestPaths.Contains(kv.Key)))
            .ToList();
    }
}
