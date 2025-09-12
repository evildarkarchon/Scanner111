using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.CLI.Test.Infrastructure;
using System.IO;

namespace Scanner111.CLI.Test.Services;

public class FileWatcherTests : CliTestBase, IDisposable
{
    private readonly FileWatcher _fileWatcher;
    private readonly string _testDirectory;
    private readonly string _testFile;
    private readonly List<string> _capturedEvents;
    private readonly SemaphoreSlim _eventSemaphore;

    public FileWatcherTests()
    {
        var logger = Substitute.For<ILogger<FileWatcher>>();
        _fileWatcher = new FileWatcher(logger);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileWatcherTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(_testFile, "Initial content");
        
        _capturedEvents = new List<string>();
        _eventSemaphore = new SemaphoreSlim(0, 1);
        
        _fileWatcher.FileChanged += OnFileChanged;
        _fileWatcher.Error += OnError;
    }

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        _capturedEvents.Add($"Changed:{e.FullPath}:{e.ChangeType}");
        _eventSemaphore.Release();
    }

    private void OnError(object? sender, FileWatcherErrorEventArgs e)
    {
        _capturedEvents.Add($"Error:{e.Message}");
        _eventSemaphore.Release();
    }

    [Fact]
    public void StartWatching_WithValidFile_StartsMonitoring()
    {
        // Act
        _fileWatcher.StartWatching(_testFile);

        // Assert
        _fileWatcher.IsWatching.Should().BeTrue();
        _fileWatcher.WatchedPath.Should().Be(_testFile);
        _fileWatcher.WatchedFiles.Should().ContainSingle();
        _fileWatcher.WatchedFiles.First().Should().Be(_testFile);
    }

    [Fact]
    public void StartWatching_WithDirectory_MonitorsAllFiles()
    {
        // Arrange
        var file2 = Path.Combine(_testDirectory, "test2.log");
        File.WriteAllText(file2, "Content");

        // Act
        _fileWatcher.StartWatching(_testDirectory);

        // Assert
        _fileWatcher.IsWatching.Should().BeTrue();
        _fileWatcher.WatchedPath.Should().Be(_testDirectory);
        _fileWatcher.WatchedFiles.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void StopWatching_WhenWatching_StopsMonitoring()
    {
        // Arrange
        _fileWatcher.StartWatching(_testFile);

        // Act
        _fileWatcher.StopWatching();

        // Assert
        _fileWatcher.IsWatching.Should().BeFalse();
        _fileWatcher.WatchedPath.Should().BeEmpty();
        _fileWatcher.WatchedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task FileChanged_WhenFileModified_RaisesEvent()
    {
        // Arrange
        _fileWatcher.SetDebounceDelay(50); // Short delay for testing
        _fileWatcher.StartWatching(_testFile);
        await Task.Delay(100); // Let watcher initialize

        // Act
        await File.AppendAllTextAsync(_testFile, "\nNew content");
        
        // Wait for event with timeout
        var eventReceived = await _eventSemaphore.WaitAsync(5000);

        // Assert
        eventReceived.Should().BeTrue("Event should be raised within timeout");
        _capturedEvents.Should().ContainSingle();
        _capturedEvents[0].Should().StartWith($"Changed:{_testFile}");
    }

    [Fact]
    public async Task FileChanged_WithDebouncing_CoalescesMultipleChanges()
    {
        // Arrange
        _fileWatcher.SetDebounceDelay(200); // Longer debounce for testing
        _fileWatcher.StartWatching(_testFile);
        await Task.Delay(100);

        // Act - Multiple rapid changes
        for (int i = 0; i < 5; i++)
        {
            await File.AppendAllTextAsync(_testFile, $"\nLine {i}");
            await Task.Delay(50); // Less than debounce delay
        }

        // Wait for event
        var eventReceived = await _eventSemaphore.WaitAsync(5000);

        // Assert - Should only get one event due to debouncing
        eventReceived.Should().BeTrue();
        _capturedEvents.Should().ContainSingle("Multiple changes should be coalesced");
    }

    [Fact]
    public void StartWatchingMultiple_WithMultipleFiles_MonitorsAll()
    {
        // Arrange
        var file2 = Path.Combine(_testDirectory, "test2.log");
        var file3 = Path.Combine(_testDirectory, "test3.log");
        File.WriteAllText(file2, "Content2");
        File.WriteAllText(file3, "Content3");

        // Act
        _fileWatcher.StartWatchingMultiple(new[] { _testFile, file2, file3 });

        // Assert
        _fileWatcher.IsWatching.Should().BeTrue();
        _fileWatcher.WatchedFiles.Should().HaveCount(3);
        _fileWatcher.WatchedFiles.Should().Contain(_testFile);
        _fileWatcher.WatchedFiles.Should().Contain(file2);
        _fileWatcher.WatchedFiles.Should().Contain(file3);
    }

    [Fact]
    public void StopWatching_WithSpecificFile_RemovesOnlyThatFile()
    {
        // Arrange
        var file2 = Path.Combine(_testDirectory, "test2.log");
        File.WriteAllText(file2, "Content2");
        _fileWatcher.StartWatchingMultiple(new[] { _testFile, file2 });

        // Act
        _fileWatcher.StopWatching(_testFile);

        // Assert
        _fileWatcher.IsWatching.Should().BeTrue();
        _fileWatcher.WatchedFiles.Should().ContainSingle();
        _fileWatcher.WatchedFiles.Should().Contain(file2);
        _fileWatcher.WatchedFiles.Should().NotContain(_testFile);
    }

    [Fact]
    public async Task ForceCheckAsync_TriggersImmediateCheck()
    {
        // Arrange
        _fileWatcher.SetDebounceDelay(5000); // Very long delay
        _fileWatcher.StartWatching(_testFile);
        await Task.Delay(100);
        
        await File.AppendAllTextAsync(_testFile, "\nForced check content");

        // Act
        await _fileWatcher.ForceCheckAsync();

        // Assert - Event should fire immediately despite long debounce
        var eventReceived = await _eventSemaphore.WaitAsync(1000);
        eventReceived.Should().BeTrue("ForceCheck should trigger immediate event");
        _capturedEvents.Should().ContainSingle();
    }

    [Fact]
    public void StartWatching_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.log");

        // Act & Assert
        var act = () => _fileWatcher.StartWatching(nonExistentFile);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public async Task FileDeleted_RaisesChangeEvent()
    {
        // Arrange
        var tempFile = Path.Combine(_testDirectory, "delete-test.log");
        File.WriteAllText(tempFile, "To be deleted");
        _fileWatcher.SetDebounceDelay(50);
        _fileWatcher.StartWatching(tempFile);
        await Task.Delay(100);

        // Act
        File.Delete(tempFile);

        // Wait for event
        var eventReceived = await _eventSemaphore.WaitAsync(5000);

        // Assert
        eventReceived.Should().BeTrue();
        _capturedEvents.Should().ContainSingle();
        _capturedEvents[0].Should().Contain("Deleted");
    }

    [Fact]
    public void Dispose_ReleasesAllResources()
    {
        // Arrange
        _fileWatcher.StartWatching(_testFile);

        // Act
        _fileWatcher.Dispose();

        // Assert
        _fileWatcher.IsWatching.Should().BeFalse();
        _fileWatcher.WatchedFiles.Should().BeEmpty();
        
        // Attempting to start watching after dispose should not work
        var act = () => _fileWatcher.StartWatching(_testFile);
        act.Should().NotThrow();
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        _eventSemaphore?.Dispose();
        
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
        
        base.Dispose();
    }
}