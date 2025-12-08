using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Scanner111.ViewModels;
using Scanner111.Views;
using System.Threading.Tasks;
using System.Linq;

namespace Scanner111.Services;

public class DialogService : IDialogService
{
    public async Task ShowSettingsDialogAsync(SettingsViewModel viewModel)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = viewModel
        };

        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            await settingsWindow.ShowDialog(mainWindow);
        }
        else
        {
            settingsWindow.Show();
        }
    }

    public async Task<string?> ShowFolderPickerAsync(string title, string? initialDirectory = null)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            return null;
        }

        var storageProvider = mainWindow.StorageProvider;

        IStorageFolder? startLocation = null;
        if (!string.IsNullOrWhiteSpace(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
        {
            startLocation = await storageProvider.TryGetFolderFromPathAsync(initialDirectory);
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startLocation,
            AllowMultiple = false
        });

        return result.FirstOrDefault()?.Path.LocalPath;
    }

    private static Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
