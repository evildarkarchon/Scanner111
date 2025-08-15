using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI.Views;

/// <summary>
///     A window for managing and displaying application settings.
///     This window allows users to configure application preferences and performs interactive file or folder selections.
/// </summary>
/// <remarks>
///     The <see cref="SettingsWindow" /> is a part of an Avalonia UI project and is typically displayed as a modal dialog
///     from the main application window.
/// </remarks>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        if (DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.ShowFilePickerAsync = ShowFilePickerAsync;
            viewModel.ShowFolderPickerAsync = ShowFolderPickerAsync;
        }
    }

    /// <summary>
    ///     Handles changes to the DataContext property of the <see cref="SettingsWindow" />.
    /// </summary>
    /// <param name="e">
    ///     Event arguments associated with the DataContext change.
    /// </param>
    /// <remarks>
    ///     This method ensures the <see cref="SettingsWindowViewModel" /> associated with the DataContext
    ///     is updated with the necessary delegates for interacting with file and folder pickers.
    /// </remarks>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not SettingsWindowViewModel viewModel) return;
        viewModel.ShowFilePickerAsync = ShowFilePickerAsync;
        viewModel.ShowFolderPickerAsync = ShowFolderPickerAsync;
    }

    /// <summary>
    ///     Displays a file picker dialog that allows the user to select a single file and returns its local path.
    /// </summary>
    /// <param name="title">
    ///     The title of the file picker dialog.
    /// </param>
    /// <param name="fileTypeFilter">
    ///     A filter specifying allowed file types, such as "*.log" for log files.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is the local path of the selected file,
    ///     or an empty string if no file is selected.
    /// </returns>
    private async Task<string> ShowFilePickerAsync(string title, string fileTypeFilter)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return "";

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = GetFileTypes(fileTypeFilter)
        });

        return files.FirstOrDefault()?.Path.LocalPath ?? "";
    }

    /// <summary>
    ///     Opens a folder picker dialog that allows the user to select a folder.
    /// </summary>
    /// <param name="title">
    ///     The title of the folder picker dialog.
    /// </param>
    /// <returns>
    ///     A <see cref="Task{TResult}" /> representing the asynchronous operation,
    ///     with a result of the selected folder's local path as a string,
    ///     or an empty string if no folder is selected.
    /// </returns>
    private async Task<string> ShowFolderPickerAsync(string title)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return "";

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        IStorageFolder? first = null;
        foreach (var folder in folders)
        {
            first = folder;
            break;
        }

        return first?.Path.LocalPath ?? "";
    }

    /// <summary>
    ///     Generates a list of file types based on the specified filter pattern.
    /// </summary>
    /// <param name="filter">
    ///     A string representing the file type pattern to filter (e.g., "*.log").
    /// </param>
    /// <returns>
    ///     A list of <see cref="FilePickerFileType" /> objects representing the allowable file types for the picker.
    /// </returns>
    private static List<FilePickerFileType> GetFileTypes(string filter)
    {
        return filter switch
        {
            "*.log" =>
            [
                new FilePickerFileType("Log Files") { Patterns = ["*.log"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] }
            ],
            _ => [new FilePickerFileType("All Files") { Patterns = ["*.*"] }]
        };
    }
}