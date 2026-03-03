using Gtk;

namespace FileKeeper.Gtk.Dialogs.Generics;

[Obsolete("Use DialogBuilder instead for more flexible dialog creation.", true)]
public static class GenericDialogs
{
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