using System;

namespace FileKeeper.UI.Models;

public record FileEntryDto(
    string RelativePath,
    string Hash,
    long Size,
    string FormattedSize,
    DateTime LastModified
);