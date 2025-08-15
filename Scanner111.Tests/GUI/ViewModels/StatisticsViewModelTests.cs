using FluentAssertions;
using Moq;
using Scanner111.Core.Services;
using Scanner111.GUI.ViewModels;

namespace Scanner111.Tests.GUI.ViewModels;

[Collection("GUI Tests")]
public class StatisticsViewModelTests
{
    private readonly Mock<IStatisticsService> _mockStatsService;
    private readonly StatisticsViewModel _viewModel;

    public StatisticsViewModelTests()
    {
        _mockStatsService = new Mock<IStatisticsService>();
        _viewModel = new StatisticsViewModel(_mockStatsService.Object);
    }

    [Fact]
    public async Task LoadStatisticsCommand_LoadsDataSuccessfully()
    {
        // Arrange
        var summary = new StatisticsSummary
        {
            TotalScans = 100,
            SuccessfulScans = 75,
            FailedScans = 25,
            SolveRate = 75.0,
            AverageProcessingTime = TimeSpan.FromSeconds(1.5),
            TotalProcessingTime = TimeSpan.FromMinutes(2.5),
            TotalIssuesFound = 250
        };

        var recentScans = new List<ScanStatistics>
        {
            new()
            {
                Timestamp = DateTime.Now.AddHours(-1),
                LogFilePath = "test1.log",
                GameType = "Fallout4",
                TotalIssuesFound = 5,
                CriticalIssues = 2,
                WasSolved = true,
                ProcessingTime = TimeSpan.FromSeconds(1)
            },
            new()
            {
                Timestamp = DateTime.Now.AddHours(-2),
                LogFilePath = "test2.log",
                GameType = "Skyrim",
                TotalIssuesFound = 3,
                CriticalIssues = 1,
                WasSolved = false,
                ProcessingTime = TimeSpan.FromSeconds(2)
            }
        };

        _mockStatsService.Setup(x => x.GetSummaryAsync())
            .ReturnsAsync(summary);
        _mockStatsService.Setup(x => x.GetRecentScansAsync(It.IsAny<int>()))
            .ReturnsAsync(recentScans);

        // Act - Execute the load command explicitly
        await _viewModel.LoadStatisticsAsync();

        // Assert
        _viewModel.TotalScans.Should().Be(100);
        _viewModel.SuccessfulScans.Should().Be(75);
        _viewModel.FailedScans.Should().Be(25);
        _viewModel.TotalIssues.Should().Be(250);
        _viewModel.AvgProcessingTime.Should().Be("1500ms");
        _viewModel.RecentScans.Should().HaveCount(2);
    }

    [Fact]
    public async Task Properties_UpdateCorrectly()
    {
        // Arrange
        var summary = new StatisticsSummary
        {
            TotalScans = 50,
            SuccessfulScans = 40,
            FailedScans = 10,
            TotalIssuesFound = 100,
            AverageProcessingTime = TimeSpan.FromMilliseconds(2500)
        };

        _mockStatsService.Setup(x => x.GetSummaryAsync())
            .ReturnsAsync(summary);
        _mockStatsService.Setup(x => x.GetRecentScansAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<ScanStatistics>());

        // Act - Create new view model
        var vm = new StatisticsViewModel(_mockStatsService.Object);
        await Task.Delay(100); // Give async constructor task time to complete

        // Assert
        vm.TotalScans.Should().Be(50);
        vm.SuccessfulScans.Should().Be(40);
        vm.FailedScans.Should().Be(10);
        vm.TotalIssues.Should().Be(100);
        vm.AvgProcessingTime.Should().Be("2500ms");
    }

    [Fact]
    public void RecentScans_ShowsProperlyFormattedData()
    {
        // Arrange
        var scanStats = new ScanStatistics
        {
            Timestamp = new DateTime(2024, 1, 15, 10, 30, 45),
            LogFilePath = @"C:\Games\Fallout4\crash_2024.log",
            TotalIssuesFound = 10,
            CriticalIssues = 3
        };

        // Act
        var scanVm = new ScanStatisticsViewModel(scanStats);

        // Assert
        scanVm.LogFilePath.Should().Be("crash_2024.log");
        scanVm.Timestamp.Should().Be("2024-01-15 10:30:45");
        scanVm.IssuesFoundText.Should().Be("10 issues found (3 critical)");
    }

    // Export functionality test removed - feature not yet implemented

    [Fact]
    public void PropertyChanged_IsRaisedForTotalScans()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.TotalScans)) propertyChangedRaised = true;
        };

        // Act
        _viewModel.TotalScans = 100;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_IsRaisedForSuccessfulScans()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.SuccessfulScans)) propertyChangedRaised = true;
        };

        // Act
        _viewModel.SuccessfulScans = 75;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_IsRaisedForAvgProcessingTime()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.AvgProcessingTime)) propertyChangedRaised = true;
        };

        // Act
        _viewModel.AvgProcessingTime = "1234ms";

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public async Task LoadStatistics_HandlesExceptionGracefully()
    {
        // Arrange
        _mockStatsService.Setup(x => x.GetSummaryAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act - Create new view model, which catches exceptions in constructor
        var vm = new StatisticsViewModel(_mockStatsService.Object);
        await Task.Delay(100); // Give async constructor task time to complete

        // Assert - Should handle error gracefully with default values
        vm.TotalScans.Should().Be(0);
        vm.SuccessfulScans.Should().Be(0);
        vm.FailedScans.Should().Be(0);
    }

    [Fact]
    public void ObservableCollection_UpdatesProperly()
    {
        // Arrange
        var scanStats = new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test.log",
            TotalIssuesFound = 5,
            CriticalIssues = 2
        };

        // Act
        _viewModel.RecentScans.Add(new ScanStatisticsViewModel(scanStats));

        // Assert
        _viewModel.RecentScans.Should().HaveCount(1);
        _viewModel.RecentScans[0].LogFilePath.Should().Be("test.log");
    }
}