using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Gtk.Dialogs.Generics;
using Gtk;

namespace FileKeeper.Gtk.Dialogs;

public class RestoreDialog : Dialog
{
    private readonly ListStore _versionStore;
    private readonly TreeView _versionTreeView;
    private readonly Label _selectedDestLabel;
    
    private readonly IBackupRepository _backupRepository;

    public RestoreDialog(Window parent, string? currentDestination, IBackupRepository backupRepository)
        : base("Restore backup", parent, DialogFlags.Modal)
    {
        _backupRepository = backupRepository;
        
        SetDefaultSize(700, 400);

        Box mainBox = new Box(Orientation.Vertical, 10);
        mainBox.Margin = 10;

        // Version selection
        Label versionLabel = new Label("<b>Select a Backup Version:</b>");
        versionLabel.UseMarkup = true;
        versionLabel.Xalign = 0;
        mainBox.PackStart(versionLabel, false, false, 0);

        // Create version list with columns: ID, Date, Created, Updated, Deleted
        _versionStore = new ListStore(typeof(long), typeof(string), typeof(int), typeof(int), typeof(int));
        _versionTreeView = new TreeView(_versionStore);

        // Column: ID (hidden but stored)
        TreeViewColumn idCol = new TreeViewColumn();
        idCol.Title = "ID";
        idCol.Visible = false;
        CellRendererText idCell = new CellRendererText();
        idCol.PackStart(idCell, true);
        idCol.AddAttribute(idCell, "text", 0);
        _versionTreeView.AppendColumn(idCol);

        // Column: Date/Time
        TreeViewColumn dateCol = new TreeViewColumn();
        dateCol.Title = "Date/Time";
        CellRendererText dateCell = new CellRendererText();
        dateCol.PackStart(dateCell, true);
        dateCol.AddAttribute(dateCell, "text", 1);
        dateCol.MinWidth = 180;
        _versionTreeView.AppendColumn(dateCol);

        // Column: Created Files
        TreeViewColumn createdCol = new TreeViewColumn();
        createdCol.Title = "Created";
        CellRendererText createdCell = new CellRendererText();
        createdCol.PackStart(createdCell, true);
        createdCol.AddAttribute(createdCell, "text", 2);
        createdCol.MinWidth = 80;
        _versionTreeView.AppendColumn(createdCol);

        // Column: Updated Files
        TreeViewColumn updatedCol = new TreeViewColumn();
        updatedCol.Title = "Updated";
        CellRendererText updatedCell = new CellRendererText();
        updatedCol.PackStart(updatedCell, true);
        updatedCol.AddAttribute(updatedCell, "text", 3);
        updatedCol.MinWidth = 80;
        _versionTreeView.AppendColumn(updatedCol);

        // Column: Deleted Files
        TreeViewColumn deletedCol = new TreeViewColumn();
        deletedCol.Title = "Deleted";
        CellRendererText deletedCell = new CellRendererText();
        deletedCol.PackStart(deletedCell, true);
        deletedCol.AddAttribute(deletedCell, "text", 4);
        deletedCol.MinWidth = 80;
        _versionTreeView.AppendColumn(deletedCol);

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

        _selectedDestLabel = new Label(currentDestination ?? Environment.GetEnvironmentVariable("HOME") ?? "/home");
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

    public (bool Success, long BackupId, string DestinationFolder) GetSelectedDestination()
    {
        TreeIter iter;
        if (_versionTreeView.Selection.GetSelected(out iter))
        {
            long backupId = (long)_versionStore.GetValue(iter, 0);
            string selectedDest = _selectedDestLabel.Text;

            return (true, backupId, selectedDest);
        }

        return (false, 0, string.Empty);
    }

    public async Task LoadBackupsAsync(CancellationToken token = default)
    {
        var result = await _backupRepository.GetAllAsync(token);

        if (result.IsError)
        {
            new DialogBuilder()
                .WithParent(this)
                .AsError()
                .WithPrimaryText("Failed to load backups from database")
                .WithSecondaryText(string.Join("\n", result.Errors.Select(e => e.Description)))
                .ShowAndDestroy();
            
            return;
        }

        _versionStore.Clear();

        foreach (var backup in result.Value.OrderByDescending(b => b.CreatedAt))
        {
            _versionStore.AppendValues(
                backup.Id,
                backup.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                backup.CreatedFiles,
                backup.UpdatedFiles,
                backup.DeletedFiles
            );
        }
    }
}