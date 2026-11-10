namespace FileKeeper.Core.Models;

public enum IssueSeverity
{
    /// <summary>Data cannot be restored (missing or corrupted content, unreadable manifest).</summary>
    Error,

    /// <summary>Nothing is lost, but the archive holds unexpected data (e.g. unreferenced entries).</summary>
    Warning,
}

public sealed record VerificationIssue(IssueSeverity Severity, string Message);

/// <summary>Outcome of <see cref="Services.BackupEngine.VerifyArchive"/>.</summary>
public sealed class VerificationResult
{
    public int SnapshotCount { get; init; }

    /// <summary>Number of distinct stored file contents that were read and hashed.</summary>
    public int CheckedBlobCount { get; init; }

    public long CheckedBytes { get; init; }

    public IReadOnlyList<VerificationIssue> Issues { get; init; } = [];

    public int ErrorCount => Issues.Count(i => i.Severity == IssueSeverity.Error);

    public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);

    public bool IsHealthy => ErrorCount == 0;
}
