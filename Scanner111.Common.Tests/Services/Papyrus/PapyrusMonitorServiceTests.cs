using FluentAssertions;
using Moq;
using Scanner111.Common.Models.Papyrus;
using Scanner111.Common.Services.Papyrus;

namespace Scanner111.Common.Tests.Services.Papyrus;

/// <summary>
/// Tests for PapyrusMonitorService.
/// </summary>
public class PapyrusMonitorServiceTests
{
    private readonly Mock<IPapyrusLogReader> _mockReader;
    private readonly PapyrusMonitorService _service;

    public PapyrusMonitorServiceTests()
    {
        _mockReader = new Mock<IPapyrusLogReader>();
        _service = new PapyrusMonitorService(_mockReader.Object);
    }

    [Fact]
    public void Constructor_WithNullReader_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PapyrusMonitorService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logReader");
    }

    [Fact]
    public void IsMonitoring_BeforeStart_ReturnsFalse()
    {
        // Assert
        _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public void StartMonitoring_SetsIsMonitoringToTrue()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);

        // Act
        _service.StartMonitoring(logPath);

        // Assert
        _service.IsMonitoring.Should().BeTrue();

        // Cleanup
        _service.StopMonitoring();
    }

    [Fact]
    public void StopMonitoring_SetsIsMonitoringToFalse()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);
        _service.StartMonitoring(logPath);

        // Act
        _service.StopMonitoring();

        // Assert
        _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public void StartMonitoring_WhenAlreadyMonitoring_DoesNotRestart()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);
        _service.StartMonitoring(logPath);

        // Act - try to start again
        _service.StartMonitoring(logPath);

        // Assert - should still be monitoring without error
        _service.IsMonitoring.Should().BeTrue();

        // Cleanup
        _service.StopMonitoring();
    }

    [Fact]
    public async Task StartMonitoring_EmitsInitialEmptyStats()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);

        PapyrusStats? receivedStats = null;
        _service.StatsUpdated += stats => receivedStats = stats;

        // Act
        _service.StartMonitoring(logPath);

        // Give time for initial stats to be emitted
        await Task.Delay(100);

        // Assert
        receivedStats.Should().NotBeNull();
        receivedStats!.Dumps.Should().Be(0);
        receivedStats.Stacks.Should().Be(0);
        receivedStats.Warnings.Should().Be(0);
        receivedStats.Errors.Should().Be(0);

        // Cleanup
        _service.StopMonitoring();
    }

    [Fact]
    public async Task StartMonitoring_WhenFileNotFound_EmitsError()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath))
            .Throws(new FileNotFoundException("File not found", logPath));

        string? receivedError = null;
        _service.ErrorOccurred += error => receivedError = error;

        // Act
        _service.StartMonitoring(logPath);

        // Give time for error to be emitted
        await Task.Delay(100);

        // Assert
        receivedError.Should().NotBeNull();
        receivedError.Should().Contain("not found");

        // Cleanup
        _service.StopMonitoring();
    }

    [Fact]
    public async Task Monitoring_OnlyEmitsOnStatsChange()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);

        var unchangedStats = new PapyrusStats
        {
            Timestamp = DateTime.Now,
            Dumps = 1,
            Stacks = 2,
            Warnings = 0,
            Errors = 0
        };

        _mockReader.Setup(r => r.ReadNewContentAsync(
                logPath,
                It.IsAny<long>(),
                It.IsAny<PapyrusStats>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PapyrusReadResult(unchangedStats, 200));

        var emitCount = 0;
        _service.StatsUpdated += _ => emitCount++;

        // Act
        _service.StartMonitoring(logPath, 50); // Fast polling for test

        // Wait for multiple poll cycles
        await Task.Delay(300);

        // Assert - should emit once for initial, but not repeatedly for unchanged stats
        // Note: The initial emit is empty stats, then we get the first poll with unchangedStats
        // After that, we shouldn't get more emits since stats don't change
        emitCount.Should().BeLessThanOrEqualTo(3); // Initial + first poll + maybe one more

        // Cleanup
        _service.StopMonitoring();
    }

    [Fact]
    public async Task Monitoring_EmitsOnEachStatsChange()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);

        var callCount = 0;
        _mockReader.Setup(r => r.ReadNewContentAsync(
                logPath,
                It.IsAny<long>(),
                It.IsAny<PapyrusStats>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                var stats = new PapyrusStats
                {
                    Timestamp = DateTime.Now,
                    Dumps = callCount,
                    Stacks = callCount * 2,
                    Warnings = 0,
                    Errors = 0
                };
                return new PapyrusReadResult(stats, 100 + callCount * 10);
            });

        var receivedStats = new List<PapyrusStats>();
        _service.StatsUpdated += stats => receivedStats.Add(stats);

        // Act
        _service.StartMonitoring(logPath, 50); // Fast polling for test

        // Wait for multiple poll cycles
        await Task.Delay(250);

        // Assert - should have received multiple different stats
        receivedStats.Count.Should().BeGreaterThan(1);

        // Cleanup
        _service.StopMonitoring();
    }

    [Fact]
    public void Dispose_StopsMonitoring()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);
        _service.StartMonitoring(logPath);

        // Act
        _service.Dispose();

        // Assert
        _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);
        _service.StartMonitoring(logPath);

        // Act & Assert - should not throw
        _service.Dispose();
        var act = () => _service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void StartMonitoring_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _service.Dispose();

        // Act
        var act = () => _service.StartMonitoring(@"C:\Test\Papyrus.0.log");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Monitoring_HandlesReadErrors_Gracefully()
    {
        // Arrange
        var logPath = @"C:\Test\Papyrus.0.log";
        _mockReader.Setup(r => r.GetFileEndPosition(logPath)).Returns(100);

        _mockReader.Setup(r => r.ReadNewContentAsync(
                logPath,
                It.IsAny<long>(),
                It.IsAny<PapyrusStats>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Read error"));

        string? receivedError = null;
        _service.ErrorOccurred += error => receivedError = error;

        // Act
        _service.StartMonitoring(logPath, 50);

        // Wait for poll
        await Task.Delay(150);

        // Assert - should have received error but still be monitoring
        receivedError.Should().NotBeNull();
        receivedError.Should().Contain("error");
        _service.IsMonitoring.Should().BeTrue();

        // Cleanup
        _service.StopMonitoring();
    }
}
