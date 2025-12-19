using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Linq;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    [Reactive] public ViewModelBase CurrentPage { get; set; } = null!;

    // Window properties
    [Reactive] public double WindowWidth { get; set; } = 1000;
    [Reactive] public double WindowHeight { get; set; } = 600;

    private bool _isResizingForNav;

    public ReactiveCommand<Unit, Unit> GoToHomeCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToArticlesCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToBackupsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public MainWindowViewModel(
        ISettingsService settingsService,
        Func<SettingsViewModel> settingsViewModelFactory,
        Func<HomePageViewModel> homePageViewModelFactory,
        Func<ResultsViewModel> resultsViewModelFactory,
        Func<BackupsViewModel> backupsViewModelFactory,
        Func<AboutViewModel> aboutViewModelFactory)
    {
        _settingsService = settingsService;

        // Size persistence
        this.WhenAnyValue(x => x.WindowWidth, x => x.WindowHeight)
            .Skip(1) // Skip initial load
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(size =>
            {
                if (_isResizingForNav) return;
                _settingsService.SaveWindowSize(CurrentPage.GetType().Name, size.Item1, size.Item2);
                _settingsService.Save();
            });

        GoToHomeCommand = ReactiveCommand.Create(() => NavigateTo(homePageViewModelFactory()));
        GoToArticlesCommand = ReactiveCommand.Create(() => NavigateTo(new ArticlesViewModel()));
        GoToBackupsCommand = ReactiveCommand.Create(() => NavigateTo(backupsViewModelFactory()));
        GoToResultsCommand = ReactiveCommand.Create(() => NavigateTo(resultsViewModelFactory()));
        GoToSettingsCommand = ReactiveCommand.Create(() => NavigateTo(settingsViewModelFactory()));
        GoToAboutCommand = ReactiveCommand.Create(() => NavigateTo(aboutViewModelFactory()));

        ExitCommand = ReactiveCommand.Create(ExitApplication);

        // Default page
        NavigateTo(homePageViewModelFactory());
    }

    private void NavigateTo(ViewModelBase page)
    {
        _isResizingForNav = true;

        // Get saved size for this page type
        var (w, h) = _settingsService.GetWindowSize(page.GetType().Name);
        WindowWidth = w;
        WindowHeight = h;

        CurrentPage = page;

        _isResizingForNav = false;
    }

    private static void ExitApplication()
    {
        Environment.Exit(0);
    }
}
