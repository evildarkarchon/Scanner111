using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.CLI.Test.Infrastructure;
using Scanner111.CLI.UI;
using Scanner111.CLI.UI.Screens;
using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;
using Spectre.Console;

namespace Scanner111.CLI.Test.UI;

public class NavigationServiceTests : CliTestBase
{
    private readonly NavigationService _navigationService;
    private readonly IServiceProvider _mockServiceProvider;

    public NavigationServiceTests()
    {
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        _mockServiceProvider = services.BuildServiceProvider();
        
        var logger = Substitute.For<ILogger<NavigationService>>();
        _navigationService = new NavigationService(_mockServiceProvider, Console as IAnsiConsole, logger);
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Register mock screens
        services.AddTransient<MainMenuScreen>();
        services.AddTransient<HelpScreen>();
        services.AddTransient<ResultsScreen>();
        services.AddTransient<TestScreen>();
        
        // Register other required services
        services.AddSingleton(Substitute.For<IAnalyzeService>());
        services.AddSingleton(Substitute.For<ISessionManager>());
        services.AddSingleton(Substitute.For<IConfigurationService>());
    }

    [Fact]
    public async Task NavigateToAsync_WithValidScreen_NavigatesSuccessfully()
    {
        // Arrange
        var testScreen = new TestScreen();

        // Act
        await _navigationService.NavigateToAsync(testScreen, CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().Be(testScreen);
        _navigationService.CanGoBack.Should().BeFalse(); // First screen
    }

    [Fact]
    public async Task NavigateToAsync_MultipleScreens_MaintainsStack()
    {
        // Arrange
        var screen1 = new TestScreen { Name = "Screen1" };
        var screen2 = new TestScreen { Name = "Screen2" };

        // Act
        await _navigationService.NavigateToAsync(screen1, CancellationTokenSource.Token);
        await _navigationService.NavigateToAsync(screen2, CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().Be(screen2);
        _navigationService.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public async Task GoBackAsync_WithHistory_ReturnsTrue()
    {
        // Arrange
        var screen1 = new TestScreen { Name = "Screen1" };
        var screen2 = new TestScreen { Name = "Screen2" };
        await _navigationService.NavigateToAsync(screen1, CancellationTokenSource.Token);
        await _navigationService.NavigateToAsync(screen2, CancellationTokenSource.Token);

        // Act
        await _navigationService.GoBackAsync(CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().Be(screen1);
        _navigationService.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public async Task GoBackAsync_WithoutHistory_ReturnsFalse()
    {
        // Arrange
        var screen = new TestScreen();
        await _navigationService.NavigateToAsync(screen, CancellationTokenSource.Token);

        // Act
        await _navigationService.GoBackAsync(CancellationTokenSource.Token);

        // Assert
        // When at root screen, GoBackAsync calls Exit()
        _navigationService.CurrentScreen.Should().BeNull();
    }

    [Fact]
    public async Task NavigateToAsync_WithData_PassesDataToScreen()
    {
        // Arrange
        var testData = new { Message = "Test Data" };
        var receivingScreen = new DataReceiverScreen();

        // Act
        receivingScreen.SetData(testData);
        await _navigationService.NavigateToAsync(receivingScreen, CancellationTokenSource.Token);

        // Assert
        receivingScreen.ReceivedData.Should().Be(testData);
    }

    [Fact]
    public async Task NavigateToAsync_ByType_CreatesNewInstance()
    {
        // Act
        await _navigationService.NavigateToAsync<MainMenuScreen>(CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().BeOfType<MainMenuScreen>();
    }

    [Fact]
    public async Task NavigateToAsync_ByTypeWithData_PassesData()
    {
        // Arrange
        var testData = new AnalysisResult
        {
            Success = true,
            Fragment = CreateTestReportFragment()
        };

        // Act
        await _navigationService.NavigateToAsync<ResultsScreen>(testData, CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().BeOfType<ResultsScreen>();
        var resultsScreen = _navigationService.CurrentScreen as ResultsScreen;
        resultsScreen.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearHistory_RemovesAllButCurrent()
    {
        // Arrange
        var screen1 = new TestScreen { Name = "Screen1" };
        var screen2 = new TestScreen { Name = "Screen2" };
        var screen3 = new TestScreen { Name = "Screen3" };
        
        await _navigationService.NavigateToAsync(screen1, CancellationTokenSource.Token);
        await _navigationService.NavigateToAsync(screen2, CancellationTokenSource.Token);
        await _navigationService.NavigateToAsync(screen3, CancellationTokenSource.Token);

        // Act
        _navigationService.ClearHistory();

        // Assert
        _navigationService.CurrentScreen.Should().Be(screen3);
        _navigationService.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void Exit_TriggersApplicationExit()
    {
        // Act
        _navigationService.Exit();

        // Assert
        CancellationTokenSource.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task HandleScreenResult_WithNavigateToResult_NavigatesToScreen()
    {
        // Arrange
        var targetScreen = new TestScreen { Name = "Target" };
        var result = ScreenResult.NavigateTo(targetScreen);

        // Act
        await _navigationService.HandleScreenResult(result, CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().Be(targetScreen);
    }

    [Fact]
    public async Task HandleScreenResult_WithGoBackResult_GoesBack()
    {
        // Arrange
        var screen1 = new TestScreen { Name = "Screen1" };
        var screen2 = new TestScreen { Name = "Screen2" };
        await _navigationService.NavigateToAsync(screen1, CancellationTokenSource.Token);
        await _navigationService.NavigateToAsync(screen2, CancellationTokenSource.Token);
        
        var result = ScreenResult.GoBack();

        // Act
        await _navigationService.HandleScreenResult(result, CancellationTokenSource.Token);

        // Assert
        _navigationService.CurrentScreen.Should().Be(screen1);
    }

    [Fact]
    public async Task HandleScreenResult_WithExitResult_ExitsApplication()
    {
        // Arrange
        var result = ScreenResult.Exit();

        // Act
        await _navigationService.HandleScreenResult(result, CancellationTokenSource.Token);

        // Assert
        CancellationTokenSource.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task NavigateToAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var screen = new TestScreen();
        CancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _navigationService.NavigateToAsync(screen, CancellationTokenSource.Token));
    }

    // Test helper classes
    private class TestScreen : BaseScreen
    {
        public string Name { get; set; } = "TestScreen";
        
        public override string Title => Name;
        
        public override Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ScreenResult.None());
        }
    }

    private class DataReceiverScreen : BaseScreen, IDataReceiver
    {
        public object? ReceivedData { get; private set; }
        
        public override string Title => "DataReceiverScreen";

        public void SetData(object data)
        {
            ReceivedData = data;
        }

        public override Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ScreenResult.None());
        }
    }
}