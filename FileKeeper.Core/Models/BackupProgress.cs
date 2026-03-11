namespace FileKeeper.Core.Models;

public class BackupProgress
{
    public int CurrentFileIndex { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public string CurrentFolder { get; set; } = string.Empty;
    
    public double Percentage => TotalFiles > 0 ? (CurrentFileIndex / (double)TotalFiles) * 100 : 0;
    
    public string Message => 
        $"Processing: {CurrentFolder} ({CurrentFileIndex}/{TotalFiles}) - {CurrentFileName}";
}