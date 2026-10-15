namespace FileKeeper.Core.Models;

/// <summary>A stored version of a file to restore, and where to write it.</summary>
/// <param name="Path">Archive path of the file.</param>
/// <param name="StoredIn">Id of the snapshot holding the version to restore.</param>
/// <param name="TargetPath">Path relative to the restore folder ('/' separated).</param>
public sealed record RestoreItem(string Path, int StoredIn, string TargetPath);
