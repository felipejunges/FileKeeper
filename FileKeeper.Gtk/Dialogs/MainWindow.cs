using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Gtk.Dialogs.Generics;
using Gtk;

namespace FileKeeper.Gtk.Dialogs;

public class MainWindow : Window
{
    private Label _pathLabel;
    private ListStore _foldersStore;
    private ListStore _filesStore;
    private ListStore _versionsStore;
    private TreeView _foldersView;
    private TreeView _filesView;
    private TreeView _versionsView;
    private Label _fileInfoLabel;
    private string _currentPath;
    private Stack<string> _navigationHistory;
    private string? _selectedFilePath;

    private CancellationTokenSource? _defaultCancellationTokenSource;

    private readonly IConfigurationService _configurationService;
    private readonly ICreateBackupUseCase _createBackupUseCase;
    private readonly IRestoreBackupUseCase _restoreBackupUseCase;

    public MainWindow(
        IConfigurationService configurationService,
        ICreateBackupUseCase createBackupUseCase,
        IRestoreBackupUseCase restoreBackupUseCase)
        : base("File Browser with Versions")
    {
        _configurationService = configurationService;
        _createBackupUseCase = createBackupUseCase;
        _restoreBackupUseCase = restoreBackupUseCase;

        _defaultCancellationTokenSource = new CancellationTokenSource();

        SetDefaultSize(1000, 700);
        SetPosition(WindowPosition.Center);

        DeleteEvent += (_, _) =>
        {
            _defaultCancellationTokenSource?.Cancel();
            Application.Quit();
        };

        _currentPath = Environment.GetEnvironmentVariable("HOME") ?? "/home";
        _navigationHistory = new Stack<string>();

        Box mainLayout = new Box(Orientation.Vertical, 5);
        mainLayout.Margin = 5;

        // Navigation Bar
        Box navBox = new Box(Orientation.Horizontal, 5);

        Button backBtn = new Button("← Back");
        backBtn.Clicked += (o, e) => NavigateBack();
        navBox.PackStart(backBtn, false, false, 0);

        Button homeBtn = new Button("🏠 Home");
        homeBtn.Clicked += (o, e) => NavigateTo(Environment.GetEnvironmentVariable("HOME") ?? "/home");
        navBox.PackStart(homeBtn, false, false, 0);

        Button rootBtn = new Button("📁 Root");
        rootBtn.Clicked += (o, e) => NavigateTo("/");
        navBox.PackStart(rootBtn, false, false, 0);

        // Add spacer
        navBox.PackStart(new Label(""), true, true, 0);

        // Configuration button
        Button configBtn = new Button("⚙️ Configuration");
        configBtn.Clicked += async (_, _) => await ShowConfigurationDialogAsync(_defaultCancellationTokenSource.Token);
        navBox.PackStart(configBtn, false, false, 0);

        // Create Backup button
        Button backupBtn = new Button("💾 Create Backup");
        backupBtn.Clicked += async (_, _) => await CreateNewBackupAsync(_defaultCancellationTokenSource.Token);
        navBox.PackStart(backupBtn, false, false, 0);

        // Restore button
        Button restoreBtn = new Button("↻ Restore");
        restoreBtn.Clicked += async (_, _) => await ShowRestoreDialogAsync(_defaultCancellationTokenSource.Token);
        navBox.PackStart(restoreBtn, false, false, 0);

        mainLayout.PackStart(navBox, false, false, 0);

        // Path Display
        _pathLabel = new Label(_currentPath);
        _pathLabel.UseMarkup = false;
        _pathLabel.Xalign = 0;
        mainLayout.PackStart(_pathLabel, false, false, 0);

        // Main Content Area (Two Panels)
        Paned hpaned = new Paned(Orientation.Horizontal);
        hpaned.Position = 300;

        // Left Panel - Folders
        Box leftPanel = new Box(Orientation.Vertical, 5);
        Label foldersLabel = new Label("<b>Folders</b>");
        foldersLabel.UseMarkup = true;
        leftPanel.PackStart(foldersLabel, false, false, 0);

        ScrolledWindow foldersScroll = new ScrolledWindow();
        foldersScroll.ShadowType = ShadowType.In;
        _foldersStore = new ListStore(typeof(string), typeof(string)); // Name, Path
        _foldersView = new TreeView(_foldersStore);

        TreeViewColumn folderNameCol = new TreeViewColumn();
        folderNameCol.Title = "Name";
        CellRendererText folderCell = new CellRendererText();
        folderNameCol.PackStart(folderCell, true);
        folderNameCol.AddAttribute(folderCell, "text", 0);
        _foldersView.AppendColumn(folderNameCol);

        _foldersView.RowActivated += OnFolderDoubleClicked;
        foldersScroll.Add(_foldersView);
        leftPanel.PackStart(foldersScroll, true, true, 0);

        hpaned.Add1(leftPanel);

        // Right Panel - Files
        Box rightPanel = new Box(Orientation.Vertical, 5);
        Label filesLabel = new Label("<b>Files</b>");
        filesLabel.UseMarkup = true;
        rightPanel.PackStart(filesLabel, false, false, 0);

        ScrolledWindow filesScroll = new ScrolledWindow();
        filesScroll.ShadowType = ShadowType.In;
        _filesStore = new ListStore(typeof(string), typeof(string)); // Name, Path
        _filesView = new TreeView(_filesStore);

        TreeViewColumn fileNameCol = new TreeViewColumn();
        fileNameCol.Title = "Name";
        CellRendererText fileCell = new CellRendererText();
        fileNameCol.PackStart(fileCell, true);
        fileNameCol.AddAttribute(fileCell, "text", 0);
        _filesView.AppendColumn(fileNameCol);

        _filesView.Selection.Changed += OnFileSelected;
        filesScroll.Add(_filesView);
        rightPanel.PackStart(filesScroll, true, true, 0);

        hpaned.Add2(rightPanel);

        mainLayout.PackStart(hpaned, true, true, 0);

        // Resume Area - Now with interactive versions
        Frame resumeFrame = new Frame("Versioning Resume");

        Box resumeBox = new Box(Orientation.Vertical, 5);
        resumeBox.Margin = 5;

        // File info label
        _fileInfoLabel = new Label("Select a file to see versions.");
        _fileInfoLabel.UseMarkup = true;
        _fileInfoLabel.Justify = Justification.Left;
        _fileInfoLabel.Xalign = 0;
        resumeBox.PackStart(_fileInfoLabel, false, false, 0);

        // Versions list
        ScrolledWindow versionsScroll = new ScrolledWindow();
        versionsScroll.ShadowType = ShadowType.In;
        versionsScroll.HeightRequest = 150;

        _versionsStore = new ListStore(typeof(int), typeof(string), typeof(string)); // Version number, Date, Commit Message
        _versionsView = new TreeView(_versionsStore);

        // Version number column
        TreeViewColumn versionCol = new TreeViewColumn();
        versionCol.Title = "Version";
        CellRendererText versionCell = new CellRendererText();
        versionCol.PackStart(versionCell, false);
        versionCol.AddAttribute(versionCell, "text", 0);
        _versionsView.AppendColumn(versionCol);

        // Date column
        TreeViewColumn dateCol = new TreeViewColumn();
        dateCol.Title = "Date";
        CellRendererText dateCell = new CellRendererText();
        dateCol.PackStart(dateCell, false);
        dateCol.AddAttribute(dateCell, "text", 1);
        _versionsView.AppendColumn(dateCol);

        // Commit message column
        TreeViewColumn messageCol = new TreeViewColumn();
        messageCol.Title = "Commit Message";
        CellRendererText messageCell = new CellRendererText();
        messageCol.PackStart(messageCell, true);
        messageCol.AddAttribute(messageCell, "text", 2);
        _versionsView.AppendColumn(messageCol);

        _versionsView.RowActivated += OnVersionRowActivated;
        versionsScroll.Add(_versionsView);

        resumeBox.PackStart(versionsScroll, true, true, 0);

        // Action buttons area
        Box actionBox = new Box(Orientation.Horizontal, 5);

        Button actionBtn = new Button("Execute Action");
        actionBtn.Clicked += (o, e) => OnVersionActionClicked();
        actionBox.PackStart(actionBtn, false, false, 0);

        resumeBox.PackStart(actionBox, false, false, 0);

        resumeFrame.Add(resumeBox);
        mainLayout.PackStart(resumeFrame, false, false, 5);

        Add(mainLayout);

        RefreshCurrentDirectory();
    }

    private void NavigateTo(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                _navigationHistory.Push(_currentPath);
                _currentPath = path;
                RefreshCurrentDirectory();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error navigating to {path}: {ex.Message}");
        }
    }

    private void NavigateBack()
    {
        if (_navigationHistory.Count > 0)
        {
            _currentPath = _navigationHistory.Pop();
            RefreshCurrentDirectory();
        }
    }

    private void RefreshCurrentDirectory()
    {
        _pathLabel.Text = _currentPath;
        _foldersStore.Clear();
        _filesStore.Clear();
        _versionsStore.Clear();
        _fileInfoLabel.Text = "Select a file to see versions.";
        _selectedFilePath = null;

        try
        {
            // Add parent directory option if not at root
            if (_currentPath != "/")
            {
                string parentPath = Directory.GetParent(_currentPath)?.FullName ?? "/";
                _foldersStore.AppendValues("..", parentPath);
            }

            // Add subdirectories
            try
            {
                foreach (string dir in Directory.GetDirectories(_currentPath).OrderBy(d => d))
                {
                    try
                    {
                        var dirName = System.IO.Path.GetFileName(dir);
                        _foldersStore.AppendValues(dirName, dir);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        /* Skip */
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }

            // Add files
            try
            {
                foreach (string file in Directory.GetFiles(_currentPath).OrderBy(f => f))
                {
                    var fileName = System.IO.Path.GetFileName(file);
                    _filesStore.AppendValues(fileName, file);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {_currentPath}: {ex.Message}");
        }
    }

    private void OnFolderDoubleClicked(object? sender, RowActivatedArgs e)
    {
        TreeIter iter;
        if (_foldersStore.GetIter(out iter, e.Path))
        {
            string path = (string)_foldersStore.GetValue(iter, 1);
            NavigateTo(path);
        }
    }

    private void OnFileSelected(object? sender, EventArgs e)
    {
        TreeIter iter;
        TreeSelection selection = (TreeSelection)sender!;
        if (selection.GetSelected(out iter))
        {
            string path = (string)_filesStore.GetValue(iter, 1);
            _selectedFilePath = path;

            // Update file info
            var filename = System.IO.Path.GetFileName(path);
            _fileInfoLabel.Markup = $"<b>Versions for: {filename}</b>";

            // Load versions into the TreeView
            var versions = FileBrowserApp.VersionService.GetFileVersions(path);
            _versionsStore.Clear();

            foreach (var version in versions)
            {
                _versionsStore.AppendValues(
                    version.VersionNumber,
                    version.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    version.CommitMessage
                );
            }
        }
    }

    private void OnVersionRowActivated(object? sender, RowActivatedArgs e)
    {
        TreeIter iter;
        if (_versionsStore.GetIter(out iter, e.Path))
        {
            int versionNumber = (int)_versionsStore.GetValue(iter, 0);
            string date = (string)_versionsStore.GetValue(iter, 1);
            string message = (string)_versionsStore.GetValue(iter, 2);

            Console.WriteLine($"Version {versionNumber} activated - Date: {date}, Message: {message}");
        }
    }

    private void OnVersionActionClicked()
    {
        TreeIter iter;
        if (_versionsView.Selection.GetSelected(out iter))
        {
            int versionNumber = (int)_versionsStore.GetValue(iter, 0);
            string date = (string)_versionsStore.GetValue(iter, 1);
            string message = (string)_versionsStore.GetValue(iter, 2);

            // Show a dialog with version information using DialogBuilder
            new DialogBuilder()
                .WithParent(this)
                .AsInfo()
                .WithPrimaryText($"Action for Version {versionNumber}")
                .WithSecondaryText($"Date: {date}\nCommit: {message}\nFile: {_selectedFilePath}")
                .ShowAndDestroy();
        }
        else
        {
            new DialogBuilder()
                .WithParent(this)
                .AsWarning()
                .WithPrimaryText("Please select a version first")
                .ShowAndDestroy();
        }
    }

    private async Task ShowConfigurationDialogAsync(CancellationToken token)
    {
        var configuration = await _configurationService.GetConfigurationAsync(token);

        // Create the configuration dialog
        using var configurationDialog = new ConfigurationDialog();
        configurationDialog.SetConfiguration(configuration);
        configurationDialog.ShowAll();

        // Handle dialog response
        if (configurationDialog.Run() == (int)ResponseType.Accept)
        {
            configuration = configurationDialog.GetConfiguration();

            var result = await _configurationService.ApplyConfigurationAsync(configuration, token);

            if (result.IsError)
            {
                new DialogBuilder()
                    .WithParent(this)
                    .AsError()
                    .WithPrimaryText("Configuration Error")
                    .WithSecondaryText(string.Join("\n", result.Errors.Select(e => e.Description)))
                    .ShowAndDestroy();
            }
        }
    }

    private async Task CreateNewBackupAsync(CancellationToken token)
    {
        var result = await _createBackupUseCase.ExecuteAsync(token);

        if (result.IsError)
        {
            new DialogBuilder()
                .WithParent(this)
                .AsError()
                .WithPrimaryText("Backup Creation Failed")
                .WithSecondaryText(string.Join("\n", result.Errors.Select(e => e.Description)))
                .ShowAndDestroy();

            return;
        }

        new DialogBuilder()
            .WithParent(this)
            .AsInfo()
            .WithPrimaryText("Create Backup")
            .WithSecondaryText(
                $"Backup creation success!\n\n{result.Value.CreatedFiles} files created.\n{result.Value.UpdatedFiles} files updated.\n{result.Value.DeletedFiles} files deleted.")
            .ShowAndDestroy();
    }

    private async Task ShowRestoreDialogAsync(CancellationToken token)
    {
        var restoreDialog = new RestoreDialog(this);
        restoreDialog.ShowAll();

        // Handle dialog response
        if (restoreDialog.Run() == (int)ResponseType.Accept)
        {
            // Get selected version
            var data = restoreDialog.GetSelectedDestination();
            if (data.Success)
            {
                var selectedVersion = data.Version;
                var selectedDest = data.DestinationFolder;

                var TEMP_BACKUP_ID = 1;

                var result = await _restoreBackupUseCase.ExecuteAsync(TEMP_BACKUP_ID, selectedDest, token);
                if (result.IsError)
                {
                    new DialogBuilder()
                        .WithParent(this)
                        .AsError()
                        .WithPrimaryText("Error restoring the backup")
                        .WithSecondaryText(string.Join("\n", result.Errors.Select(e => e.Description)))
                        .ShowAndDestroy();

                    return;
                }

                // Show confirmation message
                new DialogBuilder()
                    .WithParent(this)
                    .AsInfo()
                    .WithPrimaryText("Restore Operation")
                    .WithSecondaryText($"The restoration was a tremendous success!!\n\nThe backup was restored to: {selectedDest}")
                    .ShowAndDestroy();
            }
        }
    }
}