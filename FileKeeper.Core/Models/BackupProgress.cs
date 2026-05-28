namespace FileKeeper.Core.Models;

public abstract class BackupProgress
{
    public required string Process { get; init; } = string.Empty;

    public abstract double Percentage { get; }
    public abstract string Message { get; }
}

public class SimpleBackupProgress : BackupProgress
{
    public override double Percentage => 100;
    public override string Message => Process;
}

public class PercentageBackupProgress : BackupProgress
{
    public required int CurrentFileIndex { get; init; }
    public required int TotalFiles { get; init; }
    public required string CurrentFileName { get; init; } = string.Empty;
    public required string CurrentFolder { get; init; } = string.Empty;
    
    public override double Percentage => TotalFiles > 0 ? CurrentFileIndex * 100 / (double)TotalFiles : 0;
    
    public override string Message => $"{Process}: {CurrentFolder} ({CurrentFileIndex}/{TotalFiles}) - {CurrentFileName}";
}