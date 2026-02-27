using Gtk;

namespace FileKeeper.Gtk.Dialogs;

public static class GenericDialogs
{
    public static void ShowErrorDialog(Window parent, string format, params object?[] args)
    {
        using var errorDialog = new MessageDialog(
            parent,
            DialogFlags.Modal,
            MessageType.Error,
            ButtonsType.Ok,
            format,
            args
        );
        errorDialog.Run();
    }

    public static bool ShowConfirmDialog(Window parent, string format)
    {
        using var confirm = new MessageDialog(parent, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, format);
        var resp = confirm.Run();
        return resp == (int)ResponseType.Yes;
    }

    public static (bool, string) ShowInputDialog(Window parent, string format, bool isPassword)
    {
        string value;
        using var keyDialog = new InputDialog(parent, format, initial: "", isPassword: isPassword);
        var resp = keyDialog.Run();
        {
            value = keyDialog.Text;
        }

        return (resp == (int)ResponseType.Ok, value);
    }
}