using FileKeeper.Core.Models;
using Gtk;

namespace FileKeeper.Gtk.Dialogs;

public class ConfigurationDialog : Dialog, IDisposable
{
    private bool _disposed = false;
    
    private SpinButton _versionSpinBtn;
    private Label _selectedDbLabel;
    private ListStore _foldersConfigStore;

    public ConfigurationDialog()
    {
        SetDefaultSize(600, 500);

        _versionSpinBtn = new SpinButton(1, 100, 1);
        _selectedDbLabel = new Label(Environment.GetEnvironmentVariable("HOME") ?? "/home");
        _foldersConfigStore = new ListStore(typeof(string));
        
        Box mainBox = new Box(Orientation.Vertical, 10);
        mainBox.Margin = 10;

        // Folders Section
        Label foldersLabel = new Label("<b>Monitored Folders:</b>");
        foldersLabel.UseMarkup = true;
        foldersLabel.Xalign = 0;
        mainBox.PackStart(foldersLabel, false, false, 0);

        // Folders list
        _foldersConfigStore.Clear();
        TreeView foldersConfigView = new TreeView(_foldersConfigStore);

        // Add sample folders
        _foldersConfigStore.AppendValues(Environment.GetEnvironmentVariable("HOME") ?? "/home");
        _foldersConfigStore.AppendValues("/etc");
        _foldersConfigStore.AppendValues("/var/log");

        TreeViewColumn folderCol = new TreeViewColumn();
        folderCol.Title = "Folder Path";
        CellRendererText folderCell = new CellRendererText();
        folderCol.PackStart(folderCell, true);
        folderCol.AddAttribute(folderCell, "text", 0);
        foldersConfigView.AppendColumn(folderCol);

        ScrolledWindow foldersScroll = new ScrolledWindow();
        foldersScroll.ShadowType = ShadowType.In;
        foldersScroll.HeightRequest = 200;
        foldersScroll.Add(foldersConfigView);
        mainBox.PackStart(foldersScroll, true, true, 0);

        // Folder management buttons
        Box folderBtnBox = new Box(Orientation.Horizontal, 5);

        Button addFolderBtn = new Button("➕ Add Folder");
        addFolderBtn.Clicked += (o, e) =>
        {
            FileChooserDialog folderChooser = new FileChooserDialog(
                "Select Folder to Monitor",
                this,
                FileChooserAction.SelectFolder
            );
            folderChooser.AddButton("Cancel", ResponseType.Cancel);
            folderChooser.AddButton("Select", ResponseType.Accept);

            if (folderChooser.Run() == (int)ResponseType.Accept)
            {
                string selectedFolder = folderChooser.Filename;
                // Check if folder already exists in list
                bool exists = false;
                TreeIter iter;
                if (_foldersConfigStore.GetIterFirst(out iter))
                {
                    do
                    {
                        string existingFolder = (string)_foldersConfigStore.GetValue(iter, 0);
                        if (existingFolder == selectedFolder)
                        {
                            exists = true;
                            break;
                        }
                    } while (_foldersConfigStore.IterNext(ref iter));
                }

                if (!exists)
                {
                    _foldersConfigStore.AppendValues(selectedFolder);
                }
                else
                {
                    MessageDialog dupDialog = new MessageDialog(
                        this,
                        DialogFlags.Modal,
                        MessageType.Warning,
                        ButtonsType.Ok,
                        "Folder already in list"
                    );
                    dupDialog.Run();
                    dupDialog.Destroy();
                }
            }

            folderChooser.Destroy();
        };
        folderBtnBox.PackStart(addFolderBtn, false, false, 0);

        Button removeFolderBtn = new Button("❌ Remove Folder");
        removeFolderBtn.Clicked += (o, e) =>
        {
            TreeIter iter;
            if (foldersConfigView.Selection.GetSelected(out iter))
            {
                _foldersConfigStore.Remove(ref iter);
            }
            else
            {
                MessageDialog warnDialog = new MessageDialog(
                    this,
                    DialogFlags.Modal,
                    MessageType.Warning,
                    ButtonsType.Ok,
                    "Please select a folder to remove"
                );
                warnDialog.Run();
                warnDialog.Destroy();
            }
        };
        folderBtnBox.PackStart(removeFolderBtn, false, false, 0);

        mainBox.PackStart(folderBtnBox, false, false, 0);

        // Separator
        Separator separator = new Separator(Orientation.Horizontal);
        mainBox.PackStart(separator, false, false, 0);

        // Version Retention Section
        Label versionLabel = new Label("<b>Version Retention:</b>");
        versionLabel.UseMarkup = true;
        versionLabel.Xalign = 0;
        mainBox.PackStart(versionLabel, false, false, 0);

        Box versionBox = new Box(Orientation.Horizontal, 10);

        Label keepVersionsLabel = new Label("Number of versions to keep:");
        versionBox.PackStart(keepVersionsLabel, false, false, 0);

        _versionSpinBtn.Value = 5; // Default value
        _versionSpinBtn.WidthRequest = 80;
        versionBox.PackStart(_versionSpinBtn, false, false, 0);

        mainBox.PackStart(versionBox, false, false, 0);

        // Database File Location Section
        Label dbLabel = new Label("<b>Database File Location:</b>");
        dbLabel.UseMarkup = true;
        dbLabel.Xalign = 0;
        mainBox.PackStart(dbLabel, false, false, 0);

        Box dbBox = new Box(Orientation.Horizontal, 5);

        _selectedDbLabel.Xalign = 0;
        dbBox.PackStart(_selectedDbLabel, true, true, 0);

        Button browseDbBtn = new Button("Browse...");
        browseDbBtn.Clicked += (o, e) =>
        {
            FileChooserDialog dbChooser = new FileChooserDialog(
                "Select Database File Location",
                this,
                FileChooserAction.SelectFolder
            );
            dbChooser.AddButton("Cancel", ResponseType.Cancel);
            dbChooser.AddButton("Select", ResponseType.Accept);

            if (dbChooser.Run() == (int)ResponseType.Accept)
            {
                _selectedDbLabel.Text = dbChooser.Filename;
            }

            dbChooser.Destroy();
        };
        dbBox.PackStart(browseDbBtn, false, false, 0);

        mainBox.PackStart(dbBox, false, false, 0);

        // Add main box to dialog content area
        ContentArea.PackStart(mainBox, true, true, 0);

        // Add buttons
        AddButton("Cancel", ResponseType.Cancel);
        AddButton("Save", ResponseType.Accept);
    }
    
    public Configuration GetConfiguration()
    {
        return new Configuration
        {
            VersionsToKeep = (int)_versionSpinBtn.Value,
            DatabaseLocation = _selectedDbLabel.Text,
            MonitoredFolders = GetFoldersFromStore()
        };
    }
    
    public void SetConfiguration(Configuration config)
    {
        _versionSpinBtn.Value = config.VersionsToKeep;
        _selectedDbLabel.Text = config.DatabaseLocation;

        _foldersConfigStore.Clear();
        foreach (var folder in config.MonitoredFolders)
        {
            _foldersConfigStore.AppendValues(folder);
        }
    }
    
    private List<string> GetFoldersFromStore()
    {
        var folders = new List<string>();
        TreeIter iter;
        if (_foldersConfigStore.GetIterFirst(out iter))
        {
            do
            {
                string folderPath = (string)_foldersConfigStore.GetValue(iter, 0);
                folders.Add(folderPath);
            } while (_foldersConfigStore.IterNext(ref iter));
        }
        return folders;
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

    ~ConfigurationDialog()
    {
        Dispose(false);
    }
}