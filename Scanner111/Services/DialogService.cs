using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Scanner111.Services.Interfaces;
using Scanner111.Views;

namespace Scanner111.Services;

/// Represents a service that provides various dialog operations for
/// interacting with users. This service is primarily designed for
/// displaying informational messages, error messages, confirmation
/// dialogs, file and folder pickers, as well as providing help or
/// about information.
/// This service is registered as a singleton and is implemented
/// to work with Avalonia-based UI applications. It is intended for use
/// as a centralized point for managing dialogs in the application.
public class DialogService(Window mainWindow) : IDialogService
{
    // Implement the IDialogService methods
    /// <summary>
    /// Displays an informational dialog with the specified title and message.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The informational message to display in the dialog.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowInfoDialogAsync(string title, string message)
    {
        await ShowInfoAsync(title, message);
    }

    /// <summary>
    /// Displays an error dialog with the specified title and message.
    /// </summary>
    /// <param name="title">The title of the error dialog.</param>
    /// <param name="message">The error message to display in the dialog.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowErrorDialogAsync(string title, string message)
    {
        await ShowErrorAsync(title, message);
    }

    /// <summary>
    /// Displays a confirmation dialog with a specified title, message, and customizable button text for "Yes" and "No".
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The message to display in the dialog.</param>
    /// <param name="yesText">The text for the "Yes" button. Defaults to "Yes".</param>
    /// <param name="noText">The text for the "No" button. Defaults to "No".</param>
    /// <returns>A task representing the asynchronous operation. The result indicates whether the "Yes" button was selected.</returns>
    public async Task<bool> ShowYesNoDialogAsync(string title, string message, string yesText = "Yes",
        string noText = "No")
    {
        var dialog = new ConfirmationDialog(title, message, yesText, noText);
        return await dialog.ShowDialog<bool>(mainWindow);
    }

    /// <summary>
    /// Displays the About dialog window, providing information about the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowAboutDialogAsync()
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(mainWindow);
    }

    /// <summary>
    /// Displays a help dialog with the specified title and content.
    /// </summary>
    /// <param name="title">The title of the help dialog.</param>
    /// <param name="content">The content to display in the help dialog.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowHelpAsync(string title, string content)
    {
        var dialog = new HelpDialog(title, content);
        await dialog.ShowDialog(mainWindow);
    }

    /// <summary>
    /// Displays a folder picker dialog, allowing the user to select a folder.
    /// </summary>
    /// <param name="title">The title for the folder picker dialog.</param>
    /// <param name="initialDirectory">The initial directory to display in the folder picker. Can be null to use the default directory.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the selected folder path, or null if no folder is selected.</returns>
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

        if (fileTypeFilters is { Length: > 0 })
            options.FileTypeFilter = fileTypeFilters.Select(ext => new FilePickerFileType(ext)
            {
                Patterns = new List<string> { $"*.{ext}" }
            }).ToArray();

        var result = await storageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Displays a confirmation dialog with the specified title and message.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <param name="message">The message to display in the confirmation dialog.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean result indicating the user's choice. Returns true if the user confirms, otherwise false.</returns>
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var dialog = new ConfirmationDialog(title, message);
        return await dialog.ShowDialog<bool>(mainWindow);
    }

    /// <summary>
    /// Displays an error dialog with the specified title and message.
    /// </summary>
    /// <param name="title">The title of the error dialog.</param>
    /// <param name="message">The error message to display in the dialog.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new HelpDialog($"Error: {title}", message);
        await dialog.ShowDialog(mainWindow);
    }

    /// <summary>
    /// Displays an informational dialog with the provided title and content.
    /// </summary>
    /// <param name="title">The title of the informational dialog.</param>
    /// <param name="message">The content or message to display within the dialog.</param>
    /// <returns>A task that represents the asynchronous operation of displaying the informational dialog.</returns>
    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new HelpDialog(title, message);
        await dialog.ShowDialog(mainWindow);
    }
}