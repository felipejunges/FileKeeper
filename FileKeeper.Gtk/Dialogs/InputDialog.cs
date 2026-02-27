using Gtk;

namespace FileKeeper.Gtk.Dialogs;

public class InputDialog : Dialog
{
    private readonly Entry _entry = new Entry();

    public string Text => _entry.Text;

    public InputDialog(Window parent, string title, string initial = "", bool isPassword = false) : base(title, parent,
        DialogFlags.Modal)
    {
        SetDefaultSize(360, 80);
        TransientFor = parent;
        DefaultResponse = ResponseType.Ok;

        var content = new Box(Orientation.Vertical, 6) { Margin = 8 };
        content.PackStart(new Label(title), false, false, 2);

        _entry.Text = initial;
        _entry.ActivatesDefault = true;
        _entry.Activated += (_, _) => this.Respond(ResponseType.Ok);

        // Hide text when used as a password field
        if (isPassword)
            _entry.Visibility = false;

        content.PackStart(_entry, false, false, 2);

        this.ContentArea.PackStart(content, true, true, 0);
        AddButton("Cancel", ResponseType.Cancel);
        AddButton("OK", ResponseType.Ok);
        ShowAll();
    }
}