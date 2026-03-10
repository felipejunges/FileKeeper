namespace FileKeeper.Core.Interfaces.UI;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string windowTitle, CancellationToken cancellationToken);
}