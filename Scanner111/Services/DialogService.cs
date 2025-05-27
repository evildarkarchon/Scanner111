using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Scanner111.Services;

public class DialogService : IDialogService
{
    private TopLevel? _parentWindow;

    public virtual void SetParentWindow(TopLevel parentWindow)
    {
        _parentWindow = parentWindow;
    }

    public async Task<string?> ShowFolderPickerAsync(string title, string? initialDirectory = null)
    {
        if (_parentWindow?.StorageProvider == null)
            return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        // Set initial directory if provided
        if (!string.IsNullOrWhiteSpace(initialDirectory))
            try
            {
                var initialFolder = await _parentWindow.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                if (initialFolder != null) options.SuggestedStartLocation = initialFolder;
            }
            catch
            {
                // Ignore errors when setting initial directory
            }

        var result = await _parentWindow.StorageProvider.OpenFolderPickerAsync(options);

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task ShowMessageBoxAsync(string title, string message)
    {
        if (_parentWindow == null)
            return;

        var parentWindow = _parentWindow as Window;
        if (parentWindow == null)
            return;

        var messageBox = new Window
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

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Padding = new Avalonia.Thickness(20, 5)
        };

        okButton.Click += (_, _) => messageBox.Close();
        panel.Children.Add(okButton);

        messageBox.Content = panel;

        await messageBox.ShowDialog(parentWindow);
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (_parentWindow is not Window parentWindow)
            return false;

        var result = false;
        var messageBox = new Window
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

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Padding = new Avalonia.Thickness(20, 5)
        };

        var noButton = new Button
        {
            Content = "No",
            Padding = new Avalonia.Thickness(20, 5)
        };

        yesButton.Click += (_, _) =>
        {
            result = true;
            messageBox.Close(result);
        };

        noButton.Click += (_, _) =>
        {
            result = false;
            messageBox.Close(result);
        };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        panel.Children.Add(buttonPanel);

        messageBox.Content = panel;

        await messageBox.ShowDialog(parentWindow);
        return result;
    }
}