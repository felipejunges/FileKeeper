namespace FileKeeper.Core.Models;

public class BackupProgress
{
    public required int CurrentFileIndex { get; set; }
    public required int TotalFiles { get; set; }
    public required string CurrentFileName { get; set; } = string.Empty;
    public required string CurrentFolder { get; set; } = string.Empty;
    public required ProcessType Type { get; set; }
    
    public double Percentage => TotalFiles > 0 ? CurrentFileIndex * 100 / (double)TotalFiles : 0;
    
    public string Message => $"{Type}: {CurrentFolder} ({CurrentFileIndex}/{TotalFiles}) - {CurrentFileName}";

    public enum ProcessType
    {
        Processing,
        Compressing
    }
}