using Gtk;

namespace FileKeeper.Gtk.Dialogs;

public class RestoreDialog : Dialog, IDisposable
{
    private bool _disposed = false;

    private readonly ListStore _versionStore;
    private readonly TreeView _versionTreeView;
    private readonly Label _selectedDestLabel;
    
    public RestoreDialog(Window parent)
        : base("Restore backup", parent, DialogFlags.Modal)
    {
        SetDefaultSize(500, 400);
        
        Box mainBox = new Box(Orientation.Vertical, 10);
        mainBox.Margin = 10;

        // Version selection
        Label versionLabel = new Label("<b>Select a Version:</b>");
        versionLabel.UseMarkup = true;
        versionLabel.Xalign = 0;
        mainBox.PackStart(versionLabel, false, false, 0);

        // Create version list with sample data
        _versionStore = new ListStore(typeof(string));
        _versionTreeView = new TreeView(_versionStore);

        // Sample version timestamps
        string[] versions = { "20260227113905", "20260226105401", "20260225083022", "20260224150530" };
        foreach (var version in versions)
        {
            _versionStore.AppendValues(version);
        }

        TreeViewColumn versionCol = new TreeViewColumn();
        versionCol.Title = "Version Timestamp";
        CellRendererText versionCell = new CellRendererText();
        versionCol.PackStart(versionCell, true);
        versionCol.AddAttribute(versionCell, "text", 0);
        _versionTreeView.AppendColumn(versionCol);

        ScrolledWindow versionScroll = new ScrolledWindow();
        versionScroll.ShadowType = ShadowType.In;
        versionScroll.HeightRequest = 150;
        versionScroll.Add(_versionTreeView);
        mainBox.PackStart(versionScroll, true, true, 0);

        // Destination folder selection
        Label destLabel = new Label("<b>Select Destination Folder:</b>");
        destLabel.UseMarkup = true;
        destLabel.Xalign = 0;
        mainBox.PackStart(destLabel, false, false, 0);

        Box destBox = new Box(Orientation.Horizontal, 5);

        _selectedDestLabel = new Label(Environment.GetEnvironmentVariable("HOME") ?? "/home");
        _selectedDestLabel.Xalign = 0;
        destBox.PackStart(_selectedDestLabel, true, true, 0);

        Button browseBtn = new Button("Browse...");
        browseBtn.Clicked += (o, e) =>
        {
            FileChooserDialog fileChooser = new FileChooserDialog(
                "Select Destination Folder",
                this,
                FileChooserAction.SelectFolder
            );
            fileChooser.AddButton("Cancel", ResponseType.Cancel);
            fileChooser.AddButton("Select", ResponseType.Accept);

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                _selectedDestLabel.Text = fileChooser.Filename;
            }

            fileChooser.Destroy();
        };
        destBox.PackStart(browseBtn, false, false, 0);

        mainBox.PackStart(destBox, false, false, 0);

        // Add main box to dialog content area
        ContentArea.PackStart(mainBox, true, true, 0);

        // Add buttons
        AddButton("Cancel", ResponseType.Cancel);
        AddButton("Proceed", ResponseType.Accept);
    }
    
    public (bool Success, string Version, string DestinationFolder) GetSelectedDestination()
    {
        TreeIter iter;
        if (_versionTreeView.Selection.GetSelected(out iter))
        {
            string selectedVersion = (string)_versionStore.GetValue(iter, 0);
            string selectedDest = _selectedDestLabel.Text;

            return (true, selectedVersion, selectedDest);
        }

        return (false, string.Empty, string.Empty);
    }
    
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        if (!_disposed)
        {
            if (disposing)
            {
                Destroy();
            }

            _disposed = true;
        }
    }

    ~RestoreDialog()
    {
        Dispose(false);
    }
}