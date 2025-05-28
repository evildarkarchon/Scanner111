// Enhanced Dialog Service with additional functionality

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Scanner111.Services;

// Enhanced interface with more dialog types
public interface IEnhancedDialogService : IDialogService
{
    Task<string?> ShowFilePickerAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypes = null,
        string? initialDirectory = null);

    Task<string?> ShowSaveFilePickerAsync(string title, string? defaultFileName = null,
        IReadOnlyList<FilePickerFileType>? fileTypes = null, string? initialDirectory = null);

    Task<string?> ShowInputDialogAsync(string title, string message, string? defaultValue = null);
}

// Enhanced implementation
/// <summary>
/// The EnhancedDialogService provides advanced dialog functionalities for file selection,
/// file saving, and input dialogs. It extends the base DialogService and implements the
/// IEnhancedDialogService interface. This service is intended to enhance user interaction
/// with dialogs within an Avalonia application.
/// </summary>
public class EnhancedDialogService : DialogService, IEnhancedDialogService
{
    // Store a reference to the parent window when it's set
    private Window? _parentWindowReference;

    // Override SetParentWindow to keep a reference to the window
    /// <summary>
    /// Sets the parent window for the dialog service. This method establishes
    /// a reference to the provided <see cref="TopLevel"/> object, which allows the dialog
    /// service to interact with the specified window as the parent for dialog operations.
    /// </summary>
    /// <param name="parentWindow">The <see cref="TopLevel"/> object representing the parent window
    /// for the dialog service. Typically, this is the main application window.</param>
    public override void SetParentWindow(TopLevel parentWindow)
    {
        base.SetParentWindow(parentWindow);
        _parentWindowReference = parentWindow as Window;
    }

    /// <summary>
    /// Displays a file picker dialog to allow the user to select a single file. The available file types
    /// and other dialog options can be customized through parameters. Returns the file path of the selected file,
    /// or null if no file is selected.
    /// </summary>
    /// <param name="title">The title of the file picker dialog.</param>
    /// <param name="fileTypes">An optional list of <see cref="FilePickerFileType"/> to filter the selectable file types.
    /// If null, the dialog will not apply any file type filter.</param>
    /// <param name="initialDirectory">An optional initial directory path for the file picker. If null or not valid,
    /// the dialog will use the default directory.</param>
    /// <returns>A string representing the selected file's path, or null if no file is selected.</returns>
    public async Task<string?> ShowFilePickerAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypes = null,
        string? initialDirectory = null)
    {
        if (_parentWindowReference?.StorageProvider == null)
            return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        };

        // Set initial directory if provided
        if (!string.IsNullOrWhiteSpace(initialDirectory))
            try
            {
                var initialFolder =
                    await _parentWindowReference.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                if (initialFolder != null) options.SuggestedStartLocation = initialFolder;
            }
            catch
            {
                // Ignore errors when setting initial directory
            }

        var result = await _parentWindowReference.StorageProvider.OpenFilePickerAsync(options);

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Displays a save file picker dialog to the user, allowing them to select a location and file name
    /// for saving a file. Includes customization options such as the dialog title, default file name,
    /// supported file types, and an initial directory.
    /// </summary>
    /// <param name="title">The title of the save file picker dialog.</param>
    /// <param name="defaultFileName">The default file name suggested to the user. This can be null if no default is provided.</param>
    /// <param name="fileTypes">A collection of <see cref="FilePickerFileType"/> objects defining the types of files
    /// the user can save. This can be null to allow all file types.</param>
    /// <param name="initialDirectory">The initial directory displayed in the save file picker.
    /// This can be null if no specific directory is required.</param>
    /// <returns>Returns the full path of the file selected by the user, or null if the dialog is canceled or fails to open.</returns>
    public async Task<string?> ShowSaveFilePickerAsync(string title, string? defaultFileName = null,
        IReadOnlyList<FilePickerFileType>? fileTypes = null, string? initialDirectory = null)
    {
        if (_parentWindowReference?.StorageProvider == null)
            return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = fileTypes
        };

        // Set initial directory if provided
        if (!string.IsNullOrWhiteSpace(initialDirectory))
            try
            {
                var initialFolder =
                    await _parentWindowReference.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                if (initialFolder != null) options.SuggestedStartLocation = initialFolder;
            }
            catch
            {
                // Ignore errors when setting initial directory
            }

        var result = await _parentWindowReference.StorageProvider.SaveFilePickerAsync(options);

        return result?.Path.LocalPath;
    }

    /// <summary>
    /// Displays an input dialog to the user, allowing them to provide a text input. The dialog
    /// includes a title, a message, and an optional default value for the input field.
    /// </summary>
    /// <param name="title">The title of the input dialog.</param>
    /// <param name="message">The message displayed within the dialog to guide the user.</param>
    /// <param name="defaultValue">An optional default value that pre-fills the input box. If not provided, the input box will be empty by default.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the user's input as a string if the dialog is confirmed, or null if canceled.</returns>
    public async Task<string?> ShowInputDialogAsync(string title, string message, string? defaultValue = null)
    {
        if (_parentWindowReference == null)
            return null;

        string? result = null;
        var inputDialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 15
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var textBox = new TextBox
        {
            Text = defaultValue ?? "",
            Width = 300
        };
        panel.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10
        };

        var okButton = new Button
        {
            Content = "OK",
            Padding = new Avalonia.Thickness(20, 5),
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(20, 5),
            IsCancel = true
        };

        okButton.Click += (_, _) =>
        {
            result = textBox.Text;
            inputDialog.Close(result);
        };

        cancelButton.Click += (_, _) =>
        {
            result = null;
            inputDialog.Close(result);
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        inputDialog.Content = panel;

        // Focus the text box when the dialog opens
        inputDialog.Opened += (_, _) => textBox.Focus();

        await inputDialog.ShowDialog(_parentWindowReference);
        return result;
    }
}

// Common file type filters for convenience
/// <summary>
/// The FileTypeFilters class provides pre-defined file type filters for use in file dialogs.
/// It includes common filters such as YAML files, log files, configuration files, and a generic
/// "All Files" option. These filters help streamline file selection functionality within the application.
/// </summary>
public static class FileTypeFilters
{
    public static readonly FilePickerFileType YamlFiles = new("YAML Files")
    {
        Patterns = new[] { "*.yaml", "*.yml" },
        MimeTypes = new[] { "application/x-yaml", "text/yaml" }
    };

    public static readonly FilePickerFileType LogFiles = new("Log Files")
    {
        Patterns = new[] { "*.log", "*.txt" },
        MimeTypes = new[] { "text/plain" }
    };

    public static readonly FilePickerFileType AllFiles = new("All Files")
    {
        Patterns = new[] { "*.*" }
    };

    public static readonly FilePickerFileType ConfigFiles = new("Configuration Files")
    {
        Patterns = new[] { "*.ini", "*.cfg", "*.config", "*.toml" },
        MimeTypes = new[] { "text/plain" }
    };
}

// Example usage in a ViewModel:
/*
public class ExampleViewModel : ViewModelBase
{
    private readonly IEnhancedDialogService _dialogService;

    public ReactiveCommand<Unit, Unit> ImportConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportLogsCommand { get; }

    public ExampleViewModel(IEnhancedDialogService dialogService)
    {
        _dialogService = dialogService;
        ImportConfigCommand = ReactiveCommand.CreateFromTask(ImportConfigAsync);
        ExportLogsCommand = ReactiveCommand.CreateFromTask(ExportLogsAsync);
    }

    private async Task ImportConfigAsync()
    {
        var configPath = await _dialogService.ShowFilePickerAsync(
            "Import Configuration File",
            new[] { FileTypeFilters.YamlFiles, FileTypeFilters.ConfigFiles, FileTypeFilters.AllFiles });

        if (!string.IsNullOrEmpty(configPath))
        {
            // Load configuration from file
            await LoadConfigurationFromFile(configPath);
        }
    }

    private async Task ExportLogsAsync()
    {
        var exportPath = await _dialogService.ShowSaveFilePickerAsync(
            "Export Scan Results",
            "scan-results.log",
            new[] { FileTypeFilters.LogFiles, FileTypeFilters.AllFiles });

        if (!string.IsNullOrEmpty(exportPath))
        {
            // Export logs to file
            await ExportLogsToFile(exportPath);
        }
    }
}
*/