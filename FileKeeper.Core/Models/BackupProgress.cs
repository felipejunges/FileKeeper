namespace FileKeeper.Core.Models;

public class BackupProgress
{
    public required int CurrentFileIndex { get; init; }
    public required int TotalFiles { get; init; }
    public required string CurrentFileName { get; init; } = string.Empty;
    public required string CurrentFolder { get; init; } = string.Empty;

    public double Percentage => TotalFiles > 0 ? CurrentFileIndex * 100 / (double)TotalFiles : 0;

    public string Message => $"Processing: {CurrentFolder} ({CurrentFileIndex}/{TotalFiles}) - {CurrentFileName}";
}