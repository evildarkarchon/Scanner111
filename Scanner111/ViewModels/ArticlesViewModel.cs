using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;

namespace Scanner111.ViewModels;

/// <summary>
/// Represents an article link with a name and URL.
/// </summary>
public record ArticleLink(string Name, string Url);

public class ArticlesViewModel : ViewModelBase
{
    /// <summary>
    /// Collection of useful resource links.
    /// </summary>
    public ObservableCollection<ArticleLink> Articles { get; } = new()
    {
        new("BUFFOUT 4 INSTALLATION", "https://www.nexusmods.com/fallout4/articles/3115"),
        new("FALLOUT 4 SETUP TIPS", "https://www.nexusmods.com/fallout4/articles/4141"),
        new("IMPORTANT PATCHES LIST", "https://www.nexusmods.com/fallout4/articles/3769"),
        new("BUFFOUT 4 NEXUS", "https://www.nexusmods.com/fallout4/mods/47359"),
        new("CLASSIC NEXUS", "https://www.nexusmods.com/fallout4/mods/56255"),
        new("CLASSIC GITHUB", "https://github.com/evildarkarchon/CLASSIC-Fallout4"),
        new("DDS TEXTURE SCANNER", "https://www.nexusmods.com/fallout4/mods/71588"),
        new("BETHINI PIE", "https://www.nexusmods.com/site/mods/631"),
        new("WRYE BASH", "https://www.nexusmods.com/fallout4/mods/20032")
    };

    public ReactiveCommand<string, Unit> OpenUrlCommand { get; }

    public ArticlesViewModel()
    {
        OpenUrlCommand = ReactiveCommand.Create<string>(OpenUrl);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Silently fail if browser can't be opened
        }
    }
}
