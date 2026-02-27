namespace FileKeeper.Gtk;

public class FileBrowserApp
{
    public class FileVersion
    {
        public int VersionNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? CommitMessage { get; set; }
        public string? FilePath { get; set; }
    }

    public class VersionService
    {
        public static List<FileVersion> GetFileVersions(string filePath)
        {
            var versions = new List<FileVersion>();
            if (string.IsNullOrEmpty(filePath)) return versions;
            
            var random = new Random();
            int versionCount = random.Next(1, 5);
            
            for (int i = 1; i <= versionCount; i++)
            {
                versions.Add(new FileVersion
                {
                    VersionNumber = i,
                    CreatedDate = DateTime.Now.AddDays(-i * 10),
                    CommitMessage = $"Mock Commit {i}",
                    FilePath = filePath
                });
            }
            
            return versions;
        }
    }
}