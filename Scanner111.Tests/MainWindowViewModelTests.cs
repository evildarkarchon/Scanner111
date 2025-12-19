using FluentAssertions;
using NSubstitute;
using Scanner111.Common.Services.DocsPath;
using Scanner111.Common.Services.Pastebin;
using Scanner111.Services;
using Scanner111.ViewModels;

namespace Scanner111.Tests;

/// <summary>
/// Tests for MainWindowViewModel navigation functionality.
/// </summary>
public class MainWindowViewModelTests
{
    [Fact]
    public void GoToHomeCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.GoToHomeCommand.Should().NotBeNull();
    }

    [Fact]
    public void GoToArticlesCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.GoToArticlesCommand.Should().NotBeNull();
    }

    [Fact]
    public void GoToBackupsCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.GoToBackupsCommand.Should().NotBeNull();
    }

    [Fact]
    public void GoToResultsCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.GoToResultsCommand.Should().NotBeNull();
    }

    [Fact]
    public void GoToSettingsCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.GoToSettingsCommand.Should().NotBeNull();
    }

    [Fact]
    public void ExitCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.ExitCommand.Should().NotBeNull();
    }

    [Fact]
    public void CurrentPage_InitiallyNotNull()
    {
        var viewModel = CreateViewModel();

        viewModel.CurrentPage.Should().NotBeNull();
    }

    [Fact]
    public void GoToArticlesCommand_ChangesCurrentPage()
    {
        var viewModel = CreateViewModel();
        var initialPage = viewModel.CurrentPage;

        viewModel.GoToArticlesCommand.Execute().Subscribe();

        viewModel.CurrentPage.Should().NotBeSameAs(initialPage);
        viewModel.CurrentPage.Should().BeOfType<ArticlesViewModel>();
    }

    [Fact]
    public void GoToBackupsCommand_ChangesCurrentPage()
    {
        var viewModel = CreateViewModel();

        viewModel.GoToBackupsCommand.Execute().Subscribe();

        viewModel.CurrentPage.Should().BeOfType<BackupsViewModel>();
    }

    private static MainWindowViewModel CreateViewModel()
    {
        // Create mock services for BackupsViewModel
        var backupService = Substitute.For<IBackupService>();
        var settingsService = Substitute.For<ISettingsService>();
        var dialogService = Substitute.For<IDialogService>();
        var scanExecutor = Substitute.For<Scanner111.Common.Services.Orchestration.IScanExecutor>();
        var scanResultsService = Substitute.For<IScanResultsService>();
        var docsPathDetector = Substitute.For<IDocsPathDetector>();
        var pastebinService = Substitute.For<IPastebinService>();

        backupService.BackupAsync(Arg.Any<string>())
            .Returns(new BackupResult(true, "Test"));

        // Factory that creates a mock PapyrusMonitorViewModel
        Func<PapyrusMonitorViewModel> papyrusMonitorFactory = () => null!;

        return new MainWindowViewModel(
            settingsService,
            () => null!, // Settings factory - not tested here
            () => new HomePageViewModel(scanExecutor, scanResultsService, dialogService, settingsService,
                docsPathDetector, pastebinService, papyrusMonitorFactory),
            () => null!, // Results factory - not tested here
            () => new BackupsViewModel(backupService, settingsService),
            () => null! // About factory - not tested here
        );
    }
}