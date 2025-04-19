using ReactiveUI;
using Scanner111.UI.Services;
using System.Reactive;

namespace Scanner111.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentViewModel;
    private readonly GameListViewModel _gameListViewModel;
    private readonly CrashLogListViewModel _crashLogListViewModel;
    private readonly PluginAnalysisViewModel _pluginAnalysisViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    
    public MainWindowViewModel(
        GameListViewModel gameListViewModel,
        CrashLogListViewModel crashLogListViewModel,
        PluginAnalysisViewModel pluginAnalysisViewModel,
        SettingsViewModel settingsViewModel,
        DashboardViewModel dashboardViewModel)
    {
        _gameListViewModel = gameListViewModel;
        _crashLogListViewModel = crashLogListViewModel;
        _pluginAnalysisViewModel = pluginAnalysisViewModel;
        _settingsViewModel = settingsViewModel;
        _dashboardViewModel = dashboardViewModel;
        
        _currentViewModel = _dashboardViewModel;
        
        NavigateToDashboardCommand = ReactiveCommand.Create(NavigateToDashboard);
        NavigateToGamesCommand = ReactiveCommand.Create(NavigateToGames);
        NavigateToCrashLogsCommand = ReactiveCommand.Create(NavigateToCrashLogs);
        NavigateToPluginAnalysisCommand = ReactiveCommand.Create(NavigateToPluginAnalysis);
        NavigateToSettingsCommand = ReactiveCommand.Create(NavigateToSettings);
    }
    
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
    }
    
    public ReactiveCommand<Unit, Unit> NavigateToDashboardCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToGamesCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToPluginAnalysisCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToSettingsCommand { get; }
    
    private void NavigateToDashboard()
    {
        CurrentViewModel = _dashboardViewModel;
    }
    
    private void NavigateToGames()
    {
        CurrentViewModel = _gameListViewModel;
    }
    
    private void NavigateToCrashLogs()
    {
        CurrentViewModel = _crashLogListViewModel;
    }
    
    private void NavigateToPluginAnalysis()
    {
        CurrentViewModel = _pluginAnalysisViewModel;
    }
    
    private void NavigateToSettings()
    {
        CurrentViewModel = _settingsViewModel;
    }
}
