using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Controls;
using ReactiveUI;

namespace Scanner111.ViewModels;

public class AboutDialogViewModel : ViewModelBase
{
    public AboutDialogViewModel(Window owner)
    {
        var owner1 = owner;

        OpenGitHubCommand = ReactiveCommand.Create(() =>
            OpenUrl("https://github.com/yourusername/Scanner111"));

        OpenNexusCommand = ReactiveCommand.Create(() =>
            OpenUrl("https://www.nexusmods.com/fallout4/mods/yourmodid"));

        OpenIssueTrackerCommand = ReactiveCommand.Create(() =>
            OpenUrl("https://github.com/yourusername/Scanner111/issues"));

        CloseCommand = ReactiveCommand.Create(() => owner1.Close());
    }

    public string VersionInfo => $"Version {GetVersionString()}";

    public ICommand OpenGitHubCommand { get; }
    public ICommand OpenNexusCommand { get; }
    public ICommand OpenIssueTrackerCommand { get; }
    public ICommand CloseCommand { get; }

    private string GetVersionString()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }

    private void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw new PlatformNotSupportedException("Platform not supported for opening URLs");
            }
        }
        catch (Exception ex)
        {
            // In a real application, we would log this exception
            Console.WriteLine($"Error opening URL: {ex.Message}");
        }
    }
}