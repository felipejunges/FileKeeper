namespace FileKeeper.Core.Models;

/// <summary>
/// Represents a file tracked by the application.
/// </summary>
public class FileModel
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}

