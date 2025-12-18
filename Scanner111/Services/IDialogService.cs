using System.Threading.Tasks;
using Scanner111.ViewModels;

namespace Scanner111.Services;

public interface IDialogService
{
    Task ShowSettingsDialogAsync(SettingsViewModel viewModel);

    /// <summary>
    /// Shows the Papyrus monitor dialog.
    /// </summary>
    /// <param name="viewModel">The ViewModel for the dialog.</param>
    /// <returns>A task representing the dialog lifetime.</returns>
    Task ShowPapyrusMonitorAsync(PapyrusMonitorViewModel viewModel);

    /// <summary>
    /// Shows a folder picker dialog.
    /// </summary>
    /// <param name="title">Title of the dialog.</param>
    /// <param name="initialDirectory">Optional initial directory to open.</param>
    /// <returns>The selected folder path, or null if cancelled.</returns>
    Task<string?> ShowFolderPickerAsync(string title, string? initialDirectory = null);
}
