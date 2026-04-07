using System.IO;

namespace FileKeeper.UI.Models;

public sealed record SnapshotFolderDto(
    string Key,
    string SourceDirectory,
    string RelativePath,
    int Depth,
    int FileCount)
{
    public string Name =>
        string.IsNullOrWhiteSpace(RelativePath)
            ? Path.GetFileName(SourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : Path.GetFileName(RelativePath);

    public string DisplayPath =>
        string.IsNullOrWhiteSpace(RelativePath)
            ? SourceDirectory
            : $"{SourceDirectory} / {RelativePath}";

    public double IndentLeft => Depth * 14;
}
