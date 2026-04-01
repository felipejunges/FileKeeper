using System;
using System.IO;

namespace FileKeeper.UI.Models;

public record FileEntryDto(
    string RelativePath,
    string Hash,
    long Size,
    string FormattedSize,
    DateTime LastModified
)
{
    public string FileName => Path.GetFileName(RelativePath);

    public string FileExtension =>
        string.IsNullOrWhiteSpace(Path.GetExtension(RelativePath))
            ? "FILE"
            : Path.GetExtension(RelativePath).TrimStart('.').ToUpperInvariant();

    public string LastModifiedDisplay => LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
