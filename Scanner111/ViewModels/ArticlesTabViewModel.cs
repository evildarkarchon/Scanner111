using System;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;

namespace Scanner111.ViewModels.Tabs;

public class ArticleItem
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ArticlesTabViewModel : ViewModelBase
{
    public ArticlesTabViewModel()
    {
        // Initialize the articles collection
        Articles = new ObservableCollection<ArticleItem>
        {
            new() {
                Title = "BUFFOUT 4 INSTALLATION",
                Url = "https://www.nexusmods.com/fallout4/articles/3115",
                Description = "Complete guide for installing and configuring Buffout 4"
            },
            new() {
                Title = "FALLOUT 4 SETUP TIPS",
                Url = "https://www.nexusmods.com/fallout4/articles/4141",
                Description = "Essential setup tips for optimal Fallout 4 performance"
            },
            new() {
                Title = "IMPORTANT PATCHES LIST",
                Url = "https://www.nexusmods.com/fallout4/articles/3769",
                Description = "Critical patches every Fallout 4 player should have"
            },
            new() {
                Title = "BUFFOUT 4 NEXUS",
                Url = "https://www.nexusmods.com/fallout4/mods/47359",
                Description = "Official Buffout 4 mod page on Nexus Mods"
            },
            new() {
                Title = "SCANNER 111 NEXUS",
                Url = "https://www.nexusmods.com/fallout4/mods/56255",
                Description = "Scanner 111 mod page (placeholder - would be actual page)"
            },
            new() {
                Title = "SCANNER 111 GITHUB",
                Url = "https://github.com/evildarkarchon/CLASSIC-Fallout4",
                Description = "Scanner 111 source code and development"
            },
            new() {
                Title = "DDS TEXTURE SCANNER",
                Url = "https://www.nexusmods.com/fallout4/mods/71588",
                Description = "Tool for scanning and fixing texture issues"
            },
            new() {
                Title = "BETHINI PIE",
                Url = "https://www.nexusmods.com/site/mods/631",
                Description = "Advanced INI configuration tool for Bethesda games"
            },
            new() {
                Title = "WRYE BASH",
                Url = "https://www.nexusmods.com/fallout4/mods/20032",
                Description = "Essential mod management and patch creation tool"
            }
        };

        // Initialize command
        OpenUrlCommand = ReactiveCommand.Create<string>(OpenUrl);
    }

    // Properties
    public ObservableCollection<ArticleItem> Articles { get; }

    // Commands
    public ReactiveCommand<string, Unit> OpenUrlCommand { get; }

    // Command implementations
    private void OpenUrl(string url)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                // Open URL in default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error message to user
            System.Diagnostics.Debug.WriteLine($"Failed to open URL: {url}, Error: {ex.Message}");
        }
    }

    // Helper property for description text
    public string TabDescription => 
        "Useful resources and links for modding Fallout 4 and troubleshooting common issues. " +
        "Click any button below to open the corresponding webpage in your default browser.";
}