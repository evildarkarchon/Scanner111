using System;
using System.Collections.Generic;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.ViewModels.Tabs;

public enum BackupOperation
{
    Backup,
    Restore,
    Remove
}

public class BackupCategory
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool HasBackup { get; set; }
    public DateTime? LastBackupDate { get; set; }
    public List<string> FilePatterns { get; set; } = new();
}

public class BackupsTabViewModel : ViewModelBase
{
    private BackupCategory? _selectedCategory;
    private string _statusMessage = "Ready";

    public BackupsTabViewModel()
    {
        // Initialize backup categories
        Categories = new ObservableCollection<BackupCategory>
        {
            new()
            {
                Name = "XSE",
                DisplayName = "Script Extender (F4SE)",
                FilePatterns = new List<string> { "f4se_*.exe", "f4se_*.dll", "f4se", "src", "CustomControlMap.txt" }
            },
            new()
            {
                Name = "RESHADE",
                DisplayName = "ReShade",
                FilePatterns = new List<string>
                    { "ReShade.ini", "ReShadePreset.ini", "reshade-shaders", "dxgi.dll", "d3d11.dll" }
            },
            new()
            {
                Name = "VULKAN",
                DisplayName = "Vulkan Renderer",
                FilePatterns = new List<string>
                    { "vulkan_d3d11.dll", "d3d11_vulkan.dll", "dxvk.conf", "vulkan-1.dll", "VulkanRT" }
            },
            new()
            {
                Name = "ENB",
                DisplayName = "ENB Series",
                FilePatterns = new List<string> { "enbseries", "d3d11.dll", "enblocal.ini", "enbseries.ini", "enb*.fx" }
            }
        };

        // Initialize commands
        BackupCommand = ReactiveCommand.CreateFromTask<BackupCategory>(async category =>
            await PerformBackupOperationAsync(category, BackupOperation.Backup));
        RestoreCommand = ReactiveCommand.CreateFromTask<BackupCategory>(async category =>
            await PerformBackupOperationAsync(category, BackupOperation.Restore));
        RemoveCommand = ReactiveCommand.CreateFromTask<BackupCategory>(async category =>
            await PerformBackupOperationAsync(category, BackupOperation.Remove));
        OpenBackupsFolderCommand = ReactiveCommand.Create(OpenBackupsFolder);

        // Check for existing backups
        CheckExistingBackups();
    }

    // Properties
    public ObservableCollection<BackupCategory> Categories { get; }

    public BackupCategory? SelectedCategory
    {
        get => _selectedCategory;
        set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    // Commands
    public ReactiveCommand<BackupCategory, Unit> BackupCommand { get; }
    public ReactiveCommand<BackupCategory, Unit> RestoreCommand { get; }
    public ReactiveCommand<BackupCategory, Unit> RemoveCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenBackupsFolderCommand { get; }

    // Command implementations
    private async Task PerformBackupOperationAsync(BackupCategory category, BackupOperation operation)
    {
        StatusMessage = $"Performing {operation} operation for {category.DisplayName}...";

        try
        {
            // TODO: Implement actual backup/restore/remove logic
            await Task.Delay(1000); // Simulate work

            switch (operation)
            {
                case BackupOperation.Backup:
                    category.HasBackup = true;
                    category.LastBackupDate = DateTime.Now;
                    StatusMessage = $"✅ {category.DisplayName} backed up successfully";
                    break;

                case BackupOperation.Restore:
                    StatusMessage = $"✅ {category.DisplayName} restored successfully";
                    break;

                case BackupOperation.Remove:
                    StatusMessage = $"✅ {category.DisplayName} files removed successfully";
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error during {operation} for {category.DisplayName}: {ex.Message}";
        }
    }

    private void OpenBackupsFolder()
    {
        try
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Vault Backups", "Game Files");

            // Create directory if it doesn't exist
            Directory.CreateDirectory(backupPath);

            // Open in file explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = backupPath,
                UseShellExecute = true
            });

            StatusMessage = "Opened Vault Backups folder";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error opening backups folder: {ex.Message}";
        }
    }

    private void CheckExistingBackups()
    {
        try
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Vault Backups", "Game Files");

            foreach (var category in Categories)
            {
                var categoryBackupPath = Path.Combine(backupPath, $"Backup {category.Name}");

                if (Directory.Exists(categoryBackupPath) && Directory.GetFiles(categoryBackupPath).Length > 0)
                {
                    category.HasBackup = true;

                    // Get the last write time of the most recent file
                    var mostRecentFile = Directory.GetFiles(categoryBackupPath)
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .FirstOrDefault();

                    category.LastBackupDate = mostRecentFile?.LastWriteTime;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error checking existing backups: {ex.Message}";
        }
    }

    // Helper properties for descriptions
    public string BackupDescription => "Backup files from the game folder into the Vault Backups folder.";
    public string RestoreDescription => "Restore file backup from the Vault Backups folder into the game folder.";
    public string RemoveDescription => "Remove files only from the game folder without removing existing backups.";

    public string TabDescription =>
        "Manage backups of important game modification files. Create backups before making changes, " +
        "restore previous configurations, or remove mod files while keeping backups safe.";
}