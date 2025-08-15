using FluentAssertions;
using Moq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.Tests.Services;

[Collection("Database Tests")]
public class RecentItemsServiceTests
{
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly RecentItemsService _service;
    private readonly ApplicationSettings _testSettings;

    public RecentItemsServiceTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _testSettings = new ApplicationSettings
        {
            RecentLogFiles = new List<string>(),
            RecentGamePaths = new List<string>(),
            RecentScanDirectories = new List<string>(),
            MaxRecentItems = 10
        };

        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(_testSettings);

        _service = new RecentItemsService(_mockSettingsService.Object);
    }

    [Fact]
    public void GetRecentLogFiles_ReturnsEmptyListWhenNoItems()
    {
        var items = _service.GetRecentLogFiles();

        items.Should().NotBeNull();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddRecentLogFile_AddsItemToList()
    {
        const string testPath = "C:\\test\\crash.log";

        _service.AddRecentLogFile(testPath);

        // Wait for async save
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s => s.RecentLogFiles.Contains(testPath))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AddRecentLogFile_MovesExistingItemToTop()
    {
        _testSettings.RecentLogFiles.Add("C:\\test\\old.log");
        _testSettings.RecentLogFiles.Add("C:\\test\\crash.log");

        _service.AddRecentLogFile("C:\\test\\crash.log");

        // Wait for async save
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s =>
                    s.RecentLogFiles[0] == "C:\\test\\crash.log" &&
                    s.RecentLogFiles.Count == 2)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void AddRecentLogFile_IgnoresEmptyPath()
    {
        _service.AddRecentLogFile("");
        _service.AddRecentLogFile(null!);
        _service.AddRecentLogFile("   ");

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(It.IsAny<ApplicationSettings>()),
            Times.Never);
    }

    [Fact]
    public async Task AddRecentGamePath_AddsItemToList()
    {
        const string testPath = "C:\\Games\\Fallout4";

        _service.AddRecentGamePath(testPath);

        // Wait for async save
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s => s.RecentGamePaths.Contains(testPath))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AddRecentScanDirectory_AddsItemToList()
    {
        const string testPath = "C:\\Users\\Test\\Documents\\My Games\\Fallout4";

        _service.AddRecentScanDirectory(testPath);

        // Wait for async save
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s => s.RecentScanDirectories.Contains(testPath))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ClearRecentLogFiles_RemovesAllItems()
    {
        _testSettings.RecentLogFiles.Add("test1.log");
        _testSettings.RecentLogFiles.Add("test2.log");

        _service.ClearRecentLogFiles();

        // Wait for async save
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s => s.RecentLogFiles.Count == 0)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ClearAllRecentItems_RemovesAllItemTypes()
    {
        _testSettings.RecentLogFiles.Add("test.log");
        _testSettings.RecentGamePaths.Add("C:\\Games");
        _testSettings.RecentScanDirectories.Add("C:\\Scans");

        _service.ClearAllRecentItems();

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s =>
                    s.RecentLogFiles.Count == 0 &&
                    s.RecentGamePaths.Count == 0 &&
                    s.RecentScanDirectories.Count == 0)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void RemoveRecentItem_RemovesSpecificItem()
    {
        _testSettings.RecentLogFiles.Add("test1.log");
        _testSettings.RecentLogFiles.Add("test2.log");

        var removed = _service.RemoveRecentItem(RecentItemType.LogFile, "test1.log");

        removed.Should().BeTrue();
        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s =>
                    !s.RecentLogFiles.Contains("test1.log") &&
                    s.RecentLogFiles.Contains("test2.log"))),
            Times.Once);
    }

    [Fact]
    public void RemoveRecentItem_ReturnsFalseForNonExistentItem()
    {
        var removed = _service.RemoveRecentItem(RecentItemType.LogFile, "nonexistent.log");

        removed.Should().BeFalse();
    }

    [Fact]
    public void RemoveRecentItem_IgnoresEmptyPath()
    {
        var removed = _service.RemoveRecentItem(RecentItemType.LogFile, "");

        removed.Should().BeFalse();
        _mockSettingsService.Verify(x => x.SaveSettingsAsync(It.IsAny<ApplicationSettings>()),
            Times.Never);
    }

    [Fact]
    public async Task IsFileAccessibleAsync_ReturnsTrueForExistingFile()
    {
        // Create a temp file
        var tempFile = Path.GetTempFileName();
        try
        {
            var accessible = await _service.IsFileAccessibleAsync(tempFile);
            accessible.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IsFileAccessibleAsync_ReturnsFalseForNonExistentFile()
    {
        var accessible = await _service.IsFileAccessibleAsync("C:\\nonexistent\\file.txt");
        accessible.Should().BeFalse();
    }

    [Fact]
    public async Task IsFileAccessibleAsync_ReturnsTrueForExistingDirectory()
    {
        var tempDir = Path.GetTempPath();

        var accessible = await _service.IsFileAccessibleAsync(tempDir);
        accessible.Should().BeTrue();
    }

    [Fact]
    public async Task IsFileAccessibleAsync_ReturnsFalseForEmptyPath()
    {
        var accessible = await _service.IsFileAccessibleAsync("");
        accessible.Should().BeFalse();
    }

    [Fact]
    public void RecentItemsChanged_EventFiredOnAdd()
    {
        RecentItemsChangedEventArgs? eventArgs = null;
        _service.RecentItemsChanged += (sender, args) => eventArgs = args;

        _service.AddRecentLogFile("test.log");

        eventArgs.Should().NotBeNull();
        eventArgs!.ItemType.Should().Be(RecentItemType.LogFile);
        eventArgs.AddedPath.Should().Be("test.log");
        eventArgs.RemovedPath.Should().BeNull();
    }

    [Fact]
    public void RecentItemsChanged_EventFiredOnRemove()
    {
        _testSettings.RecentLogFiles.Add("test.log");

        RecentItemsChangedEventArgs? eventArgs = null;
        _service.RecentItemsChanged += (sender, args) => eventArgs = args;

        _service.RemoveRecentItem(RecentItemType.LogFile, "test.log");

        eventArgs.Should().NotBeNull();
        eventArgs!.ItemType.Should().Be(RecentItemType.LogFile);
        eventArgs.RemovedPath.Should().Be("test.log");
        eventArgs.AddedPath.Should().BeNull();
    }

    [Fact]
    public void RecentItemsChanged_EventFiredOnClear()
    {
        var eventCount = 0;
        _service.RecentItemsChanged += (sender, args) => eventCount++;

        _service.ClearAllRecentItems();

        eventCount.Should().Be(3); // One for each item type
    }

    [Fact]
    public void GetRecentItems_ReturnsCorrectItemType()
    {
        _testSettings.RecentLogFiles.Add("test.log");

        var items = _service.GetRecentLogFiles();

        items.Should().HaveCount(1);
        items[0].Type.Should().Be(RecentItemType.LogFile);
        items[0].Path.Should().Be("test.log");
    }

    [Fact]
    public void Constructor_WithNullSettingsService_HandlesGracefully()
    {
        var service = new RecentItemsService();

        var items = service.GetRecentLogFiles();
        items.Should().NotBeNull();
        items.Should().BeEmpty();

        // Should not throw
        service.AddRecentLogFile("test.log");
        service.ClearAllRecentItems();
    }
}