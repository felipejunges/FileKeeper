using ErrorOr;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;
using File = FileKeeper.Core.Models.Entities.File;

namespace FileKeeper.Core.Interfaces.Repositories;

public interface IFileRepository
{
    Task<ErrorOr<IEnumerable<FileVersionDM>>> GetFilesWithVersionAsync(string backupPath, CancellationToken token);
    Task<ErrorOr<long>> InsertAsync(File file, CancellationToken token);
    Task<ErrorOr<long>> InsertVersionAsync(FileVersion version, CancellationToken token);
    Task<ErrorOr<int>> MarkAsDeletedAsync(List<long> idsArquivosExcluir, long backupId, CancellationToken token);
}