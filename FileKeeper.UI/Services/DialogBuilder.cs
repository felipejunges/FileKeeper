using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;

namespace FileKeeper.UI.Services;

public sealed class DialogBuilder
{
    private string _title = string.Empty;
    private string _message = string.Empty;
    private ButtonEnum _buttons = ButtonEnum.Ok;
    private Icon _icon = Icon.None;

    private DialogBuilder() { }

    public static DialogBuilder Create() => new();

    public static DialogBuilder CreateError() => Create()
        .WithIcon(Icon.Error)
        .WithButtons(ButtonEnum.Ok);

    public static DialogBuilder CreateConfirmation() => Create()
        .WithButtons(ButtonEnum.YesNo);

    public DialogBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public DialogBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public DialogBuilder WithButtons(ButtonEnum buttons)
    {
        _buttons = buttons;
        return this;
    }

    public DialogBuilder WithIcon(Icon icon)
    {
        _icon = icon;
        return this;
    }

    public async Task<ButtonResult> ShowAsync(Window owner)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            _title,
            _message,
            _buttons,
            _icon);

        return await box.ShowWindowDialogAsync(owner);
    }
    
    public async Task<ButtonResult> ShowAsync()
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            _title,
            _message,
            _buttons,
            _icon);

        return await box.ShowAsync();
    }

    public async Task<bool> ShowAndWaitForYesAsync(Window owner)
    {
        var result = await ShowAsync(owner);

        return result == ButtonResult.Yes;
    }
}