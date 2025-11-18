using FluentAssertions;
using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Tests.Models;

/// <summary>
/// Tests for configuration model classes (ScanConfig, ScanStatistics, ScanResult).
/// </summary>
public class ConfigurationModelTests
{
    [Fact]
    public void ScanConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ScanConfig();

        // Assert
        config.FcxMode.Should().BeFalse();
        config.ShowFormIdValues.Should().BeFalse();
        config.MoveUnsolvedLogs.Should().BeFalse();
        config.SimplifyLogs.Should().BeFalse();
        config.MaxConcurrent.Should().Be(50);
        config.FormIdDatabaseExists.Should().BeFalse();
        config.CustomPaths.Should().BeEmpty();
        config.RemoveList.Should().BeEmpty();
    }

    [Fact]
    public void ScanConfig_WithInitialization_SetsPropertiesCorrectly()
    {
        // Arrange
        var customPaths = new Dictionary<string, string>
        {
            { "GamePath", @"C:\Games\Fallout4" },
            { "ModsPath", @"C:\Games\Fallout4\Data" }
        };
        var removeList = new List<string> { "Item1", "Item2" };

        // Act
        var config = new ScanConfig
        {
            FcxMode = true,
            ShowFormIdValues = true,
            MoveUnsolvedLogs = true,
            SimplifyLogs = true,
            MaxConcurrent = 25,
            FormIdDatabaseExists = true,
            CustomPaths = customPaths,
            RemoveList = removeList
        };

        // Assert
        config.FcxMode.Should().BeTrue();
        config.ShowFormIdValues.Should().BeTrue();
        config.MoveUnsolvedLogs.Should().BeTrue();
        config.SimplifyLogs.Should().BeTrue();
        config.MaxConcurrent.Should().Be(25);
        config.FormIdDatabaseExists.Should().BeTrue();
        config.CustomPaths.Should().HaveCount(2);
        config.CustomPaths["GamePath"].Should().Be(@"C:\Games\Fallout4");
        config.RemoveList.Should().HaveCount(2);
    }

    [Fact]
    public void ScanConfig_IsImmutable()
    {
        // Arrange
        var config1 = new ScanConfig { FcxMode = true };

        // Act
        var config2 = config1 with { ShowFormIdValues = true };

        // Assert
        config1.FcxMode.Should().BeTrue();
        config1.ShowFormIdValues.Should().BeFalse();
        config2.FcxMode.Should().BeTrue();
        config2.ShowFormIdValues.Should().BeTrue();
    }

    [Fact]
    public void ScanStatistics_ElapsedTime_CalculatesCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddMinutes(-5);
        var stats = new ScanStatistics { ScanStartTime = startTime };

        // Act
        var elapsed = stats.ElapsedTime;

        // Assert
        elapsed.Should().BeGreaterThan(TimeSpan.FromMinutes(4.9));
        elapsed.Should().BeLessThan(TimeSpan.FromMinutes(5.1));
    }

    [Fact]
    public void ScanStatistics_WithValues_StoresCorrectly()
    {
        // Arrange & Act
        var stats = new ScanStatistics
        {
            Scanned = 100,
            Incomplete = 5,
            Failed = 2,
            TotalFiles = 107,
            ScanStartTime = new DateTime(2025, 11, 17, 12, 0, 0, DateTimeKind.Utc)
        };

        // Assert
        stats.Scanned.Should().Be(100);
        stats.Incomplete.Should().Be(5);
        stats.Failed.Should().Be(2);
        stats.TotalFiles.Should().Be(107);
        stats.ScanStartTime.Should().Be(new DateTime(2025, 11, 17, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ScanResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new ScanResult();

        // Assert
        result.FailedLogs.Should().BeEmpty();
        result.ProcessedFiles.Should().BeEmpty();
        result.ErrorMessages.Should().BeEmpty();
        result.ScanDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ScanResult_WithData_StoresCorrectly()
    {
        // Arrange
        var statistics = new ScanStatistics
        {
            Scanned = 50,
            TotalFiles = 55,
            ScanStartTime = DateTime.UtcNow
        };
        var failedLogs = new List<string> { "crash1.log", "crash2.log" };
        var processedFiles = new List<string> { "crash3.log", "crash4.log" };
        var errorMessages = new List<string> { "Error 1", "Error 2" };
        var duration = TimeSpan.FromMinutes(2);

        // Act
        var result = new ScanResult
        {
            Statistics = statistics,
            FailedLogs = failedLogs,
            ProcessedFiles = processedFiles,
            ErrorMessages = errorMessages,
            ScanDuration = duration
        };

        // Assert
        result.Statistics.Should().Be(statistics);
        result.FailedLogs.Should().HaveCount(2);
        result.ProcessedFiles.Should().HaveCount(2);
        result.ErrorMessages.Should().HaveCount(2);
        result.ScanDuration.Should().Be(duration);
    }

    [Fact]
    public void ScanResult_IsImmutable()
    {
        // Arrange
        var result1 = new ScanResult
        {
            ScanDuration = TimeSpan.FromMinutes(1)
        };

        // Act
        var result2 = result1 with
        {
            ScanDuration = TimeSpan.FromMinutes(2)
        };

        // Assert
        result1.ScanDuration.Should().Be(TimeSpan.FromMinutes(1));
        result2.ScanDuration.Should().Be(TimeSpan.FromMinutes(2));
    }
}
