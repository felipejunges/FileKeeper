using Gtk;

namespace FileKeeper.Gtk.Dialogs.Generics;

public class DialogBuilder
{
    private Window? _parent;
    private DialogFlags _flags = DialogFlags.Modal;
    private MessageType _messageType = MessageType.Info;
    private ButtonsType _buttonsType = ButtonsType.Ok;
    private string _primaryText = string.Empty;
    private string? _secondaryText;
    private string? _title;

    public DialogBuilder WithParent(Window parent)
    {
        _parent = parent;
        return this;
    }

    public DialogBuilder WithFlags(DialogFlags flags)
    {
        _flags = flags;
        return this;
    }

    public DialogBuilder WithMessageType(MessageType messageType)
    {
        _messageType = messageType;
        return this;
    }

    public DialogBuilder WithButtonsType(ButtonsType buttonsType)
    {
        _buttonsType = buttonsType;
        return this;
    }

    public DialogBuilder WithPrimaryText(string primaryText)
    {
        _primaryText = primaryText;
        return this;
    }

    public DialogBuilder WithSecondaryText(string secondaryText)
    {
        _secondaryText = secondaryText;
        return this;
    }

    public DialogBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public DialogBuilder AsInfo()
    {
        _messageType = MessageType.Info;
        return this;
    }

    public DialogBuilder AsWarning()
    {
        _messageType = MessageType.Warning;
        return this;
    }

    public DialogBuilder AsError()
    {
        _messageType = MessageType.Error;
        return this;
    }

    public DialogBuilder AsQuestion()
    {
        _messageType = MessageType.Question;
        return this;
    }

    public DialogBuilder WithOkButton()
    {
        _buttonsType = ButtonsType.Ok;
        return this;
    }

    public DialogBuilder WithYesNoButtons()
    {
        _buttonsType = ButtonsType.YesNo;
        return this;
    }

    public DialogBuilder WithOkCancelButtons()
    {
        _buttonsType = ButtonsType.OkCancel;
        return this;
    }

    public MessageDialog Build()
    {
        var dialog = new MessageDialog(
            _parent,
            _flags,
            _messageType,
            _buttonsType,
            _primaryText
        );

        if (!string.IsNullOrEmpty(_secondaryText))
        {
            dialog.SecondaryText = _secondaryText;
        }

        if (!string.IsNullOrEmpty(_title))
        {
            dialog.Title = _title;
        }

        return dialog;
    }

    public int ShowAndDestroy()
    {
        var dialog = Build();
        var response = dialog.Run();
        dialog.Destroy();
        return response;
    }
}

