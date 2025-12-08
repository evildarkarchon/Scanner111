using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;

namespace Scanner111.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly Func<SettingsViewModel> _settingsViewModelFactory;
    private readonly Func<HomePageViewModel> _homePageViewModelFactory;
    private readonly Func<ResultsViewModel> _resultsViewModelFactory;
    private readonly Func<BackupsViewModel> _backupsViewModelFactory;

    [Reactive] public ViewModelBase CurrentPage { get; set; }

    public ReactiveCommand<Unit, Unit> GoToHomeCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToArticlesCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToBackupsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public MainWindowViewModel(
        Func<SettingsViewModel> settingsViewModelFactory,
        Func<HomePageViewModel> homePageViewModelFactory,
        Func<ResultsViewModel> resultsViewModelFactory,
        Func<BackupsViewModel> backupsViewModelFactory)
    {
        _settingsViewModelFactory = settingsViewModelFactory;
        _homePageViewModelFactory = homePageViewModelFactory;
        _resultsViewModelFactory = resultsViewModelFactory;
        _backupsViewModelFactory = backupsViewModelFactory;

        GoToHomeCommand = ReactiveCommand.Create(() => { CurrentPage = _homePageViewModelFactory(); });
        GoToArticlesCommand = ReactiveCommand.Create(() => { CurrentPage = new ArticlesViewModel(); });
        GoToBackupsCommand = ReactiveCommand.Create(() => { CurrentPage = _backupsViewModelFactory(); });
        GoToResultsCommand = ReactiveCommand.Create(() => { CurrentPage = _resultsViewModelFactory(); });
        GoToSettingsCommand = ReactiveCommand.Create(() => { CurrentPage = _settingsViewModelFactory(); });

        ExitCommand = ReactiveCommand.Create(ExitApplication);

        // Default page
        CurrentPage = _homePageViewModelFactory();
    }

    private static void ExitApplication()
    {
        Environment.Exit(0);
    }
}

