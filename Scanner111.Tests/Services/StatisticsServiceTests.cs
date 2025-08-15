using FluentAssertions;
using Scanner111.Core.Services;

namespace Scanner111.Tests.Services;

[Collection("Database Tests")]
public class StatisticsServiceTests : IDisposable
{
    private readonly StatisticsService _service;
    private readonly string _testDbPath;

    public StatisticsServiceTests()
    {
        // Use a unique test database for each test run
        var tempPath = Path.Combine(Path.GetTempPath(), $"Scanner111_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);
        _testDbPath = Path.Combine(tempPath, "statistics.db");

        // Create the service with test-specific database path
        _service = new StatisticsService(_testDbPath);

        // Give the database time to initialize using async initialization
        InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _service?.Dispose();

        // Clean up test database
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);

            var dir = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private async Task InitializeAsync()
    {
        await Task.Delay(100);
    }

    [Fact]
    public async Task RecordScanAsync_StoresStatistics()
    {
        var statistics = new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "C:\\test\\crash.log",
            GameType = "Fallout4",
            TotalIssuesFound = 5,
            CriticalIssues = 2,
            WarningIssues = 2,
            InfoIssues = 1,
            ProcessingTime = TimeSpan.FromMilliseconds(1234),
            WasSolved = true,
            PrimaryIssueType = "FormID",
            IssuesByType = new Dictionary<string, int> { { "FormID", 3 }, { "Plugin", 2 } }
        };

        await _service.RecordScanAsync(statistics);

        var latest = await _service.GetLatestScanAsync();
        latest.Should().NotBeNull();
        latest!.LogFilePath.Should().Be("C:\\test\\crash.log");
        latest.GameType.Should().Be("Fallout4");
        latest.TotalIssuesFound.Should().Be(5);
        latest.WasSolved.Should().BeTrue();
    }

    [Fact]
    public async Task GetLatestScanAsync_ReturnsNullWhenNoScans()
    {
        var latest = await _service.GetLatestScanAsync();

        latest.Should().BeNull();
    }

    [Fact]
    public async Task GetRecentScansAsync_ReturnsRequestedCount()
    {
        // Add multiple scans
        for (var i = 0; i < 15; i++)
            await _service.RecordScanAsync(new ScanStatistics
            {
                Timestamp = DateTime.Now.AddMinutes(-i),
                LogFilePath = $"C:\\test\\crash{i}.log",
                GameType = "Skyrim",
                TotalIssuesFound = i
            });

        var recent = await _service.GetRecentScansAsync();

        var recentList = new List<ScanStatistics>(recent);
        recentList.Should().HaveCount(10);
        recentList[0].LogFilePath.Should().Be("C:\\test\\crash0.log"); // Most recent
    }

    [Fact]
    public async Task GetScansInDateRangeAsync_FiltersCorrectly()
    {
        var now = DateTime.Now;

        // Add scans at different times
        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = now.AddDays(-5),
            LogFilePath = "old.log",
            GameType = "Fallout4",
            TotalIssuesFound = 1
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = now.AddDays(-2),
            LogFilePath = "recent.log",
            GameType = "Fallout4",
            TotalIssuesFound = 2
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = now.AddDays(-1),
            LogFilePath = "newest.log",
            GameType = "Fallout4",
            TotalIssuesFound = 3
        });

        var results = await _service.GetScansInDateRangeAsync(
            now.AddDays(-3),
            now);

        var resultList = new List<ScanStatistics>(results);
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(s => s.LogFilePath == "recent.log");
        resultList.Should().Contain(s => s.LogFilePath == "newest.log");
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesCorrectStats()
    {
        // Add test data
        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test1.log",
            GameType = "Fallout4",
            TotalIssuesFound = 5,
            CriticalIssues = 2,
            WarningIssues = 2,
            InfoIssues = 1,
            ProcessingTime = TimeSpan.FromMilliseconds(1000),
            WasSolved = true,
            PrimaryIssueType = "FormID"
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test2.log",
            GameType = "Skyrim",
            TotalIssuesFound = 3,
            CriticalIssues = 1,
            WarningIssues = 1,
            InfoIssues = 1,
            ProcessingTime = TimeSpan.FromMilliseconds(2000),
            WasSolved = false,
            PrimaryIssueType = "Plugin"
        });

        var summary = await _service.GetSummaryAsync();

        summary.TotalScans.Should().Be(2);
        summary.SuccessfulScans.Should().Be(1);
        summary.FailedScans.Should().Be(1);
        summary.TotalIssuesFound.Should().Be(8);
        summary.TotalCriticalIssues.Should().Be(3);
        summary.TotalWarningIssues.Should().Be(3);
        summary.TotalInfoIssues.Should().Be(2);
        summary.TotalProcessingTime.Should().Be(TimeSpan.FromMilliseconds(3000));
        summary.AverageProcessingTime.Should().Be(TimeSpan.FromMilliseconds(1500));
        summary.SolveRate.Should().Be(50.0);
    }

    [Fact]
    public async Task ClearStatisticsAsync_RemovesAllData()
    {
        // Add some data
        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test.log",
            GameType = "Fallout4",
            TotalIssuesFound = 5
        });

        // Verify data exists
        var beforeClear = await _service.GetLatestScanAsync();
        beforeClear.Should().NotBeNull();

        // Clear
        await _service.ClearStatisticsAsync();

        // Verify data is gone
        var afterClear = await _service.GetLatestScanAsync();
        afterClear.Should().BeNull();
    }

    [Fact]
    public async Task GetIssueTypeStatisticsAsync_CalculatesPercentages()
    {
        // Add scans with different issue types
        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test1.log",
            GameType = "Fallout4",
            TotalIssuesFound = 5,
            PrimaryIssueType = "FormID"
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test2.log",
            GameType = "Fallout4",
            TotalIssuesFound = 3,
            PrimaryIssueType = "FormID"
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = DateTime.Now,
            LogFilePath = "test3.log",
            GameType = "Fallout4",
            TotalIssuesFound = 2,
            PrimaryIssueType = "Plugin"
        });

        var stats = await _service.GetIssueTypeStatisticsAsync();

        var statsList = new List<IssueTypeStatistics>(stats);
        statsList.Should().HaveCount(2);

        var formIdStats = statsList.Find(s => s.IssueType == "FormID");
        formIdStats.Should().NotBeNull();
        formIdStats!.Count.Should().Be(2);
        formIdStats.Percentage.Should().BeApproximately(66.67, 0.1);

        var pluginStats = statsList.Find(s => s.IssueType == "Plugin");
        pluginStats.Should().NotBeNull();
        pluginStats!.Count.Should().Be(1);
        pluginStats.Percentage.Should().BeApproximately(33.33, 0.1);
    }

    [Fact]
    public async Task GetDailyStatisticsAsync_GroupsByDay()
    {
        var now = DateTime.Now;

        // Add scans on different days
        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = now,
            LogFilePath = "today1.log",
            GameType = "Fallout4",
            TotalIssuesFound = 5,
            ProcessingTime = TimeSpan.FromMilliseconds(1000),
            WasSolved = true
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = now,
            LogFilePath = "today2.log",
            GameType = "Fallout4",
            TotalIssuesFound = 3,
            ProcessingTime = TimeSpan.FromMilliseconds(2000),
            WasSolved = false
        });

        await _service.RecordScanAsync(new ScanStatistics
        {
            Timestamp = now.AddDays(-1),
            LogFilePath = "yesterday.log",
            GameType = "Fallout4",
            TotalIssuesFound = 2,
            ProcessingTime = TimeSpan.FromMilliseconds(1500),
            WasSolved = true
        });

        var dailyStats = await _service.GetDailyStatisticsAsync(7);

        var statsList = new List<DailyStatistics>(dailyStats);
        statsList.Should().HaveCountGreaterThanOrEqualTo(2);

        var todayStats = statsList.Find(s => s.Date.Date == now.Date);
        todayStats.Should().NotBeNull();
        todayStats!.ScanCount.Should().Be(2);
        todayStats.IssuesFound.Should().Be(8);
        todayStats.IssuesSolved.Should().Be(5); // Only the solved scan's issues
        todayStats.TotalProcessingTime.Should().Be(TimeSpan.FromMilliseconds(3000));
    }

    [Fact]
    public void Dispose_HandlesMultipleCalls()
    {
        var service = new StatisticsService();

        // Should not throw
        service.Dispose();
        service.Dispose();
    }
}