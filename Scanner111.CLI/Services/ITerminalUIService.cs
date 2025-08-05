using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Services;

public interface ITerminalUIService
{
    Task<int> RunInteractiveMode();
    void ShowInteractiveMenu();
    IProgressContext CreateProgressContext(string title, int totalItems);
    void DisplayResults(ScanResult results);
    void ShowLiveStatus(string status);
    Task<T> PromptAsync<T>(string prompt, T? defaultValue = default) where T : notnull;
}