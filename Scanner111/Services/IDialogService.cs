// Scanner111/Services/IDialogService.cs

using System.Threading.Tasks;

namespace Scanner111.Services;

public interface IDialogService
{
    Task<string?> ShowFolderPickerAsync(string title, string? initialDirectory = null);
    Task ShowMessageBoxAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message);
}