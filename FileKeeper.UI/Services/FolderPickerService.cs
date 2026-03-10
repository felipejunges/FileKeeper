using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FileKeeper.Core.Interfaces.UI;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string windowTitle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var owner = desktop.MainWindow;
        if (owner?.StorageProvider is null)
        {
            return null;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = windowTitle
        });

        cancellationToken.ThrowIfCancellationRequested();
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}