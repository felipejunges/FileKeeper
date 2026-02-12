namespace FileKeeper.Core.Interfaces;

public interface IRecycleService
{
    Task RecycleBackupsAsync(CancellationToken cancellationToken);
}