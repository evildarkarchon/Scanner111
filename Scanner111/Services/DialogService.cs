using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Scanner111.Services.Interfaces;
using Scanner111.Views;

namespace Scanner111.Services
{
    public class DialogService(Window mainWindow) : IDialogService
    {
        // Implement the IDialogService methods
        public async Task ShowInfoDialogAsync(string title, string message)
        {
            await ShowInfoAsync(title, message);
        }

        public async Task ShowErrorDialogAsync(string title, string message)
        {
            await ShowErrorAsync(title, message);
        }

        public async Task<bool> ShowYesNoDialogAsync(string title, string message, string yesText = "Yes",
            string noText = "No")
        {
            return await ShowConfirmationAsync(title, message);
        }

        public async Task ShowAboutDialogAsync()
        {
            var dialog = new AboutDialog();
            await dialog.ShowDialog(mainWindow);
        }

        public async Task ShowHelpAsync(string title, string content)
        {
            var dialog = new HelpDialog(title, content);
            await dialog.ShowDialog(mainWindow);
        }

        public async Task<string?> ShowFolderPickerAsync(string title, string? initialDirectory = null)
        {
            // Use the modern StorageProvider API instead of the obsolete OpenFolderDialog
            var storageProvider = mainWindow.StorageProvider;
            var options = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }

        public async Task<string?> ShowFilePickerAsync(string title, string[]? fileTypeFilters = null,
            string? initialDirectory = null)
        {
            // Use the modern StorageProvider API instead of the obsolete OpenFileDialog
            var storageProvider = mainWindow.StorageProvider;
            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            if (fileTypeFilters != null && fileTypeFilters.Length > 0)
            {
                options.FileTypeFilter = fileTypeFilters.Select(ext => new FilePickerFileType(ext)
                {
                    Patterns = new List<string> { $"*.{ext}" }
                }).ToArray();
            }

            var result = await storageProvider.OpenFilePickerAsync(options);
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            // In a more complete implementation, we would create a custom confirmation dialog
            // For now, we'll use the Help dialog to display the message and return true
            // In a real app, you would create a proper confirmation dialog with Yes/No buttons
            var dialog = new HelpDialog(title, message);
            await dialog.ShowDialog(mainWindow);
            return true;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new HelpDialog($"Error: {title}", message);
            await dialog.ShowDialog(mainWindow);
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            var dialog = new HelpDialog(title, message);
            await dialog.ShowDialog(mainWindow);
        }
    }
}