using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.Infrastructure.Services;

public class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickSingleFolderAsync(string title, CancellationToken cancellationToken)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var topLevel = desktop.MainWindow;
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });

        cancellationToken.ThrowIfCancellationRequested();

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}

