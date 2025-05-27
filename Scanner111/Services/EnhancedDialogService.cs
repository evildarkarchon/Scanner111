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
public class EnhancedDialogService : DialogService, IEnhancedDialogService
{
    // Store a reference to the parent window when it's set
    private Window? _parentWindowReference;

    // Override SetParentWindow to keep a reference to the window
    public override void SetParentWindow(TopLevel parentWindow)
    {
        base.SetParentWindow(parentWindow);
        _parentWindowReference = parentWindow as Window;
    }

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