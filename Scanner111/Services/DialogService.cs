using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Scanner111.Services;

/// <summary>
/// Provides services for displaying dialogs, including folder pickers, message boxes,
/// and confirmation dialogs. This class is designed to interface with Avalonia UI components
/// for creating modal dialogs.
/// </summary>
public class DialogService : IDialogService
{
    private TopLevel? _parentWindow;

    /// <summary>
    /// Sets the provided <see cref="TopLevel"/> as the parent window for the dialog service.
    /// This parent window will be used as the owner for modal dialogs created by the service.
    /// </summary>
    /// <param name="parentWindow">The parent <see cref="TopLevel"/> instance to associate with the dialog service.</param>
    public virtual void SetParentWindow(TopLevel parentWindow)
    {
        _parentWindow = parentWindow;
    }

    /// <summary>
    /// Displays a folder picker dialog that allows the user to select a single folder.
    /// Optionally, an initial directory can be provided to suggest a starting location.
    /// </summary>
    /// <param name="title">The title of the folder picker dialog.</param>
    /// <param name="initialDirectory">An optional initial directory to suggest as the starting location.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the path of the selected folder
    /// as a string if a folder is selected, or null if the user cancels the dialog.
    /// </returns>
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

    /// <summary>
    /// Displays a modal message box with the specified title and message.
    /// This method creates a simple alert-style message box that requires
    /// user acknowledgment by clicking an "OK" button.
    /// </summary>
    /// <param name="title">The title of the message box.</param>
    /// <param name="message">The message content displayed within the message box.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Displays a confirmation dialog with the specified title and message, allowing the user to respond with "Yes" or "No".
    /// </summary>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <param name="message">The message displayed in the confirmation dialog.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains a boolean value indicating whether the user confirmed (true) or declined (false).</returns>
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