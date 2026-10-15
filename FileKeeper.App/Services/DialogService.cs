using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace FileKeeper.App.Services;

public interface IDialogService
{
    Task<string?> PickFolderAsync(string title);

    Task<string?> PickSaveFileAsync(string title, string suggestedName, string? extension = null);

    Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK");

    Task ShowMessageAsync(string title, string message);
}

/// <summary>Dialogs backed by the platform storage provider and small modal windows.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string? extension = null)
    {
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            ShowOverwritePrompt = true,
        };
        if (extension is not null)
        {
            options.DefaultExtension = extension;
            options.FileTypeChoices =
            [
                new FilePickerFileType($"{extension.ToUpperInvariant()} file") { Patterns = [$"*.{extension}"] },
                FilePickerFileTypes.All,
            ];
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK") =>
        ShowDialogAsync(title, message, confirmText, "Cancel");

    public Task ShowMessageAsync(string title, string message) =>
        ShowDialogAsync(title, message, "OK", null);

    private Task<bool> ShowDialogAsync(string title, string message, string confirmText, string? cancelText)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var confirm = new Button { Content = confirmText, IsDefault = true, Classes = { "accent" } };
        confirm.Click += (_, _) => dialog.Close(true);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { confirm },
        };
        if (cancelText is not null)
        {
            var cancel = new Button { Content = cancelText, IsCancel = true };
            cancel.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancel);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                buttons,
            },
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
