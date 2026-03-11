namespace FileKeeper.Core.Models;

public class RestoreProgress
{
    public int CurrentFileIndex { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public string CurrentFolder { get; set; } = string.Empty;
    
    public string Message => 
        $"Restoring: {CurrentFolder} - {CurrentFileName} ({CurrentFileIndex} files processed)";
}

