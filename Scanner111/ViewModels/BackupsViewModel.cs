using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public class BackupsViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;

    [Reactive] public string StatusText { get; set; } = string.Empty;
    [Reactive] public bool IsOperationInProgress { get; set; }

    // XSE Commands
    public ReactiveCommand<Unit, Unit> BackupXseCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreXseCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveXseCommand { get; }

    // Reshade Commands
    public ReactiveCommand<Unit, Unit> BackupReshadeCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreReshadeCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveReshadeCommand { get; }

    // Vulkan Commands
    public ReactiveCommand<Unit, Unit> BackupVulkanCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreVulkanCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveVulkanCommand { get; }

    // ENB Commands
    public ReactiveCommand<Unit, Unit> BackupEnbCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreEnbCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveEnbCommand { get; }

    // Folder Command
    public ReactiveCommand<Unit, Unit> OpenBackupsFolderCommand { get; }

    public BackupsViewModel(IBackupService backupService, ISettingsService settingsService)
    {
        _backupService = backupService;
        _settingsService = settingsService;

        // Set game folder path from settings
        UpdateGameFolderPath();

        // Observable for when operation is not in progress
        var canExecute = this.WhenAnyValue(x => x.IsOperationInProgress, inProgress => !inProgress);

        // XSE
        BackupXseCommand = ReactiveCommand.CreateFromTask(() => ExecuteBackupAsync("XSE"), canExecute);
        RestoreXseCommand = ReactiveCommand.CreateFromTask(() => ExecuteRestoreAsync("XSE"), canExecute);
        RemoveXseCommand = ReactiveCommand.CreateFromTask(() => ExecuteRemoveAsync("XSE"), canExecute);

        // Reshade
        BackupReshadeCommand = ReactiveCommand.CreateFromTask(() => ExecuteBackupAsync("RESHADE"), canExecute);
        RestoreReshadeCommand = ReactiveCommand.CreateFromTask(() => ExecuteRestoreAsync("RESHADE"), canExecute);
        RemoveReshadeCommand = ReactiveCommand.CreateFromTask(() => ExecuteRemoveAsync("RESHADE"), canExecute);

        // Vulkan
        BackupVulkanCommand = ReactiveCommand.CreateFromTask(() => ExecuteBackupAsync("VULKAN"), canExecute);
        RestoreVulkanCommand = ReactiveCommand.CreateFromTask(() => ExecuteRestoreAsync("VULKAN"), canExecute);
        RemoveVulkanCommand = ReactiveCommand.CreateFromTask(() => ExecuteRemoveAsync("VULKAN"), canExecute);

        // ENB
        BackupEnbCommand = ReactiveCommand.CreateFromTask(() => ExecuteBackupAsync("ENB"), canExecute);
        RestoreEnbCommand = ReactiveCommand.CreateFromTask(() => ExecuteRestoreAsync("ENB"), canExecute);
        RemoveEnbCommand = ReactiveCommand.CreateFromTask(() => ExecuteRemoveAsync("ENB"), canExecute);

        // Open Backups Folder
        OpenBackupsFolderCommand = ReactiveCommand.Create(OpenBackupsFolder);
    }

    private void UpdateGameFolderPath()
    {
        // Use ScanPath from settings as the game folder path for now
        // In a full implementation, this would be a dedicated game folder setting
        _backupService.GameFolderPath = !string.IsNullOrEmpty(_settingsService.ScanPath)
            ? _settingsService.ScanPath
            : null;
    }

    private async Task ExecuteBackupAsync(string category)
    {
        UpdateGameFolderPath();
        IsOperationInProgress = true;
        StatusText = $"Backing up {category} files...";

        try
        {
            var result = await _backupService.BackupAsync(category);
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private async Task ExecuteRestoreAsync(string category)
    {
        UpdateGameFolderPath();
        IsOperationInProgress = true;
        StatusText = $"Restoring {category} files...";

        try
        {
            var result = await _backupService.RestoreAsync(category);
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private async Task ExecuteRemoveAsync(string category)
    {
        UpdateGameFolderPath();
        IsOperationInProgress = true;
        StatusText = $"Removing {category} files...";

        try
        {
            var result = await _backupService.RemoveAsync(category);
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private void OpenBackupsFolder()
    {
        try
        {
            var backupsPath = _backupService.GetBackupFolderPath();

            Process.Start(new ProcessStartInfo
            {
                FileName = backupsPath,
                UseShellExecute = true
            });

            StatusText = $"Opened backups folder: {backupsPath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening backups folder: {ex.Message}";
        }
    }
}

