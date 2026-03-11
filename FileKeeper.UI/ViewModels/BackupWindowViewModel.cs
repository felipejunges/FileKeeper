using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileKeeper.Core;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UI;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models;
using FileKeeper.Core.Models.DMs;
using FileKeeper.Core.Models.Entities;
using FileKeeper.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class BackupWindowViewModel : ViewModelBase, IInitializable
{
    [ObservableProperty] private Backup? _backup;

    [ObservableProperty]
    private IEnumerable<FileInBackupDM> _createdFiles = Array.Empty<FileInBackupDM>();
    [ObservableProperty]
    private IEnumerable<FileInBackupDM> _updatedFiles = Array.Empty<FileInBackupDM>();
    [ObservableProperty]
    private IEnumerable<FileInBackupDM> _deletedFiles = Array.Empty<FileInBackupDM>();

    [ObservableProperty]
    private string _createdFilesTitle = "";
    
    [ObservableProperty]
    private string _updatedFilesTitle = "";
    
    [ObservableProperty]
    private string _deletedFilesTitle = "";
    
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private double _restoringProgress;

    [ObservableProperty] private bool _isRestoreInProgress;
    
    public event Action? RequestClose;
    private Window? _window;

    private readonly IDeleteBackupUseCase _deleteBackupUseCase;
    private readonly IRestoreBackupUseCase _restoreBackupUseCase;
    private readonly IFolderPickerService  _folderPickerService;
    private readonly IFileRepository _fileRepository;

    public BackupWindowViewModel(
        IDeleteBackupUseCase deleteBackupUseCase,
        IRestoreBackupUseCase restoreBackupUseCase,
        IFolderPickerService folderPickerService,
        IFileRepository fileRepository)
    {
        _deleteBackupUseCase = deleteBackupUseCase;
        _restoreBackupUseCase = restoreBackupUseCase;
        _folderPickerService = folderPickerService;
        _fileRepository = fileRepository;
    }

    public async Task InitializeAsync()
    {
        await UpdateFilesLists();
    }

    private async Task UpdateFilesLists()
    {
        var ct = new CancellationTokenSource().Token;
        
        var filesResult = await _fileRepository.GetFilesInBackupAsync(Backup!.Id, ct);
        var files = filesResult.IsError ? [] : filesResult.Value.ToList();
        
        CreatedFiles = files.Where(f => f.IsNew && !f.IsDeleted);
        UpdatedFiles = files.Where(f => !f.IsNew && !f.IsDeleted);
        DeletedFiles = files.Where(f => f.IsDeleted);

        CreatedFilesTitle = $"Added ({CreatedFiles.Count()})";
        UpdatedFilesTitle = $"Updated ({UpdatedFiles.Count()})";
        DeletedFilesTitle = $"Deleted ({DeletedFiles.Count()})";
    }
    
    [RelayCommand]
    private async Task DeleteBackup(CancellationToken cancellationToken)
    {
        if (Backup is null) return;

        var confirm = await DialogBuilder.CreateConfirmation()
            .WithTitle("Confirm backup deletion")
            .WithMessage($"Confirm delete backup {Backup.Id}?")
            .ShowAndWaitForYesAsync(_window!);

        if (!confirm)
            return;

        var result = await _deleteBackupUseCase.ExecuteAsync(Backup.Id, cancellationToken);

        if (result.IsError)
        {
            await DialogBuilder.CreateError()
                .WithTitle("Error while deleting backup")
                .WithMessage(result.FirstError.Description)
                .ShowAsync(_window!);

            return;
        }

        WeakReferenceMessenger.Default.Send(new BackupDeletedMessage(Backup.Id));

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task RestoreBackup(CancellationToken cancellationToken)
    {
        if (Backup is null) return;

        var destinationFolder = await _folderPickerService.PickFolderAsync("Select destination folder", cancellationToken);

        if (string.IsNullOrWhiteSpace(destinationFolder))
            return;

        var confirm = await DialogBuilder.CreateConfirmation()
            .WithTitle("Confirm backup restoration")
            .WithMessage($"Confirm restore backup {Backup.Id} into folder {destinationFolder}?")
            .ShowAndWaitForYesAsync(_window!);

        if (!confirm)
            return;
        
        IsRestoreInProgress = true;
        RestoringProgress = 0;
        StatusMessage = "Restoring backup...";

        var progress = new Progress<RestoreProgress>(report =>
        {
            RestoringProgress = report.CurrentFileIndex;
            StatusMessage = report.Message;
        });

        var result = await _restoreBackupUseCase.ExecuteAsync(Backup.Id, destinationFolder, progress, cancellationToken);
        
        IsRestoreInProgress = false;

        if (result.IsError)
        {
            StatusMessage = "Restoration failed.";
            
            await DialogBuilder.CreateError()
                .WithTitle("Error while restoring backup")
                .WithMessage(result.FirstError.Description)
                .ShowAsync(_window!);
        }
        else
        {
            StatusMessage = "Restoration completed successfully!";
        }
    }

    public void SetBackup(Backup backup)=> Backup = backup;
    public void SetWindow(Window window) => _window = window;
}