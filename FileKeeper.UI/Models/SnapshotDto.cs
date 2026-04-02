using System;
using System.Collections.Generic;
using System.Linq;
using FileKeeper.Core.Models.Entities;

namespace FileKeeper.UI.Models;

public record SnapshotDto(
    string Name,
    DateTime CreatedAtUtc,
    int FileCount,
    string FormattedTotalSize,
    IReadOnlyList<FileEntryDto> Files
)
{
    public static SnapshotDto FromEntity(Snapshot snapshot) =>
        new(
            Name: snapshot.SnapshotName,
            CreatedAtUtc: snapshot.CreatedAtUtc,
            FileCount: snapshot.FileCount,
            FormattedTotalSize: FormatSize(snapshot.TotalSize),
            Files: snapshot.Files
                .Select(f => new FileEntryDto(
                    RelativePath: f.RelativePath,
                    Hash: f.Hash,
                    Size: f.Size,
                    FormattedSize: FormatSize(f.Size),
                    LastModified: f.LastModified,
                    FoundInSnapshot: f.FoundInSnapshot))
                .ToList()
        );

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1_024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1_024.0:F1} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
        _ => $"{bytes / 1_073_741_824.0:F1} GB"
    };
}