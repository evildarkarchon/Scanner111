using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI.Views;

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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.ShowFilePickerAsync = ShowFilePickerAsync;
            viewModel.ShowFolderPickerAsync = ShowFolderPickerAsync;
        }
    }

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

    private static List<FilePickerFileType> GetFileTypes(string filter)
    {
        return filter switch
        {
            "*.log" => new List<FilePickerFileType>
            {
                new("Log Files") { Patterns = new[] { "*.log" } },
                new("All Files") { Patterns = new[] { "*.*" } }
            },
            _ => new List<FilePickerFileType>
            {
                new("All Files") { Patterns = new[] { "*.*" } }
            }
        };
    }
}