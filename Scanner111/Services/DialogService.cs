using Avalonia.Controls;
using Scanner111.ViewModels;
using Scanner111.Views;
using System.Threading.Tasks;
using System; // Required for Application.Current

namespace Scanner111.Services;

public class DialogService : IDialogService
{
    public async Task ShowSettingsDialogAsync(SettingsViewModel viewModel)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = viewModel
        };

        // Find the main window to set as owner, if available
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            await settingsWindow.ShowDialog(desktop.MainWindow);
        }
        else
        {
            // Fallback for non-desktop environments or if main window not found
            settingsWindow.Show();
        }
    }
}
