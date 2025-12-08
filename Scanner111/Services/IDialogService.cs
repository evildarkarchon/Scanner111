using System.Threading.Tasks;
using ReactiveUI;
using Scanner111.ViewModels;

namespace Scanner111.Services;

public interface IDialogService
{
    Task ShowSettingsDialogAsync(SettingsViewModel viewModel);
}
