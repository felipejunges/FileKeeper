namespace FileKeeper.Core.Interfaces;

public interface ICompressionService
{
    void CompressFiles(IList<(string FullPath, string StoredPath)> files, string backupPath, string fileNameWithoutExtension);
}