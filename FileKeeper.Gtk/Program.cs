using FileKeeper.Core.Interfaces.Persistence;
using FileKeeper.Core.Interfaces.Services;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Persistence;
using FileKeeper.Core.Persistence.Repositories;
using FileKeeper.Core.Services;
using FileKeeper.Core.UseCases;
using Gtk;
using FileKeeper.Gtk;
using FileKeeper.Gtk.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<ICriarBackupUseCase, CriarBackupUseCase>();

        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<IFileRepository, FileRepository>();

        services.AddSingleton<IConfigurationStore, ConfigurationStore>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
    })
    .Build();

var configurationService = host.Services.GetRequiredService<IConfigurationService>();
var criarBackupUseCase = host.Services.GetRequiredService<ICriarBackupUseCase>();
var databaseService = host.Services.GetRequiredService<IDatabaseService>();

var initResult = await databaseService.InitializeAsync(CancellationToken.None);
if (initResult.IsError)
{
    Console.WriteLine($"Erro ao inicializar banco: {initResult.FirstError.Description}");
    return;
}

Application.Init();

var win = new MainWindow(
    configurationService,
    criarBackupUseCase);

win.ShowAll();

Application.Run();

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
    private readonly ICriarBackupUseCase _criarBackupUseCase;
    
    public MainWindow(
        IConfigurationService configurationService,
        ICriarBackupUseCase criarBackupUseCase)
        : base("File Browser with Versions")
    {
        _configurationService = configurationService;
        _criarBackupUseCase = criarBackupUseCase;
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
        restoreBtn.Clicked += (o, e) => ShowRestoreDialog();
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

            // Show a dialog with version information
            MessageDialog dialog = new MessageDialog(
                this,
                DialogFlags.Modal,
                MessageType.Info,
                ButtonsType.Ok,
                $"Action for Version {versionNumber}"
            );
            dialog.SecondaryText = $"Date: {date}\nCommit: {message}\nFile: {_selectedFilePath}";
            dialog.Run();
            dialog.Destroy();
        }
        else
        {
            MessageDialog dialog = new MessageDialog(
                this,
                DialogFlags.Modal,
                MessageType.Warning,
                ButtonsType.Ok,
                "Please select a version first"
            );
            dialog.Run();
            dialog.Destroy();
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
                var errorMessages = string.Join("\n", result.Errors.Select(e => e.Description));
                GenericDialogs.ShowErrorDialog(this, "Configuration Error", errorMessages);
            }
        }
    }

    private async Task CreateNewBackupAsync(CancellationToken token)
    {
        var result = await _criarBackupUseCase.ExecuteAsync(token);

        if (result.IsError)
        {
            GenericDialogs.ShowErrorDialog(this, "ERRO!!!!", result.Errors);
            return;
        }

        MessageDialog dialog = new MessageDialog(
            this,
            DialogFlags.Modal,
            MessageType.Info,
            ButtonsType.Ok,
            "Create Backup"
        );
        dialog.SecondaryText =
            "Why did the backup go to the gym?\n\nBecause it wanted to make a STRONG copy! 💪\n\nBackup implementation coming soon...";
        dialog.Run();
        dialog.Destroy();
    }

    private void ShowRestoreDialog()
    {
        // Create the restore dialog
        Dialog restoreDialog = new Dialog("Restore File", this, DialogFlags.Modal);
        restoreDialog.SetDefaultSize(500, 400);

        Box mainBox = new Box(Orientation.Vertical, 10);
        mainBox.Margin = 10;

        // Version selection
        Label versionLabel = new Label("<b>Select a Version:</b>");
        versionLabel.UseMarkup = true;
        versionLabel.Xalign = 0;
        mainBox.PackStart(versionLabel, false, false, 0);

        // Create version list with sample data
        ListStore versionStore = new ListStore(typeof(string));
        TreeView versionTreeView = new TreeView(versionStore);

        // Sample version timestamps
        string[] versions = { "20260227113905", "20260226105401", "20260225083022", "20260224150530" };
        foreach (var version in versions)
        {
            versionStore.AppendValues(version);
        }

        TreeViewColumn versionCol = new TreeViewColumn();
        versionCol.Title = "Version Timestamp";
        CellRendererText versionCell = new CellRendererText();
        versionCol.PackStart(versionCell, true);
        versionCol.AddAttribute(versionCell, "text", 0);
        versionTreeView.AppendColumn(versionCol);

        ScrolledWindow versionScroll = new ScrolledWindow();
        versionScroll.ShadowType = ShadowType.In;
        versionScroll.HeightRequest = 150;
        versionScroll.Add(versionTreeView);
        mainBox.PackStart(versionScroll, true, true, 0);

        // Destination folder selection
        Label destLabel = new Label("<b>Select Destination Folder:</b>");
        destLabel.UseMarkup = true;
        destLabel.Xalign = 0;
        mainBox.PackStart(destLabel, false, false, 0);

        Box destBox = new Box(Orientation.Horizontal, 5);

        Label selectedDestLabel = new Label(Environment.GetEnvironmentVariable("HOME") ?? "/home");
        selectedDestLabel.Xalign = 0;
        destBox.PackStart(selectedDestLabel, true, true, 0);

        Button browseBtn = new Button("Browse...");
        browseBtn.Clicked += (o, e) =>
        {
            FileChooserDialog fileChooser = new FileChooserDialog(
                "Select Destination Folder",
                restoreDialog,
                FileChooserAction.SelectFolder
            );
            fileChooser.AddButton("Cancel", ResponseType.Cancel);
            fileChooser.AddButton("Select", ResponseType.Accept);

            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                selectedDestLabel.Text = fileChooser.Filename;
            }

            fileChooser.Destroy();
        };
        destBox.PackStart(browseBtn, false, false, 0);

        mainBox.PackStart(destBox, false, false, 0);

        // Add main box to dialog content area
        restoreDialog.ContentArea.PackStart(mainBox, true, true, 0);

        // Add buttons
        restoreDialog.AddButton("Cancel", ResponseType.Cancel);
        restoreDialog.AddButton("Proceed", ResponseType.Accept);

        restoreDialog.ShowAll();

        // Handle dialog response
        if (restoreDialog.Run() == (int)ResponseType.Accept)
        {
            // Get selected version
            TreeIter iter;
            if (versionTreeView.Selection.GetSelected(out iter))
            {
                string selectedVersion = (string)versionStore.GetValue(iter, 0);
                string selectedDest = selectedDestLabel.Text;

                // Show confirmation message
                MessageDialog confirmDialog = new MessageDialog(
                    this,
                    DialogFlags.Modal,
                    MessageType.Info,
                    ButtonsType.Ok,
                    "Restore Operation"
                );
                confirmDialog.SecondaryText = $"Version: {selectedVersion}\nDestination: {selectedDest}\n\nRestore will be implemented here.";
                confirmDialog.Run();
                confirmDialog.Destroy();
            }
            else
            {
                MessageDialog errorDialog = new MessageDialog(
                    this,
                    DialogFlags.Modal,
                    MessageType.Error,
                    ButtonsType.Ok,
                    "Please select a version to restore"
                );
                errorDialog.Run();
                errorDialog.Destroy();
            }
        }

        restoreDialog.Destroy();
    }
}