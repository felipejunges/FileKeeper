using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.Infrastructure.Services;

public interface IFolderPickerService
{
    Task<string?> PickSingleFolderAsync(string title, CancellationToken cancellationToken);
}

