using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileKeeper.Core;
using FileKeeper.Core.Interfaces.Repositories;
using FileKeeper.Core.Interfaces.UI;
using FileKeeper.Core.Interfaces.UseCases;
using FileKeeper.Core.Models.Entities;
using FileKeeper.UI.Models;
using FileKeeper.UI.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FileKeeper.UI.ViewModels;

public partial class BackupWindowViewModel : ViewModelBase, IInitializable
{
    [ObservableProperty] private Backup? _backup;

    [ObservableProperty]
    private IEnumerable<BackupFileVersionViewModel> _createdFiles = Array.Empty<BackupFileVersionViewModel>();
    
    [ObservableProperty]
    private IEnumerable<BackupFileVersionViewModel> _updatedFiles = Array.Empty<BackupFileVersionViewModel>();
    
    [ObservableProperty]
    private IEnumerable<BackupFileVersionViewModel> _deletedFiles = Array.Empty<BackupFileVersionViewModel>();

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

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task UpdateFilesLists()
    {
        var ct = new CancellationTokenSource().Token;
        
        var files = await _fileRepository.GetAllBackupFiles(Backup!.Id, ct);
        
        
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

        var result = await _restoreBackupUseCase.ExecuteAsync(Backup.Id, destinationFolder, cancellationToken);

        if (result.IsError)
        {
            await DialogBuilder.CreateError()
                .WithTitle("Error while restoring backup")
                .WithMessage(result.FirstError.Description)
                .ShowAsync(_window!);
        }
    }

    public void SetBackup(Backup backup)=> Backup = backup;
    public void SetWindow(Window window) => _window = window;
}