using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class ModScanningServiceTests
{
    private readonly Mock<ILogger<ModScanningService>> _mockLogger;
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly ModScanningService _service;

    public ModScanningServiceTests()
    {
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
        _mockLogger = new Mock<ILogger<ModScanningService>>();

        _service = new ModScanningService(
            _mockYamlSettingsCache.Object,
            _mockLogger.Object,
            true);
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_ShouldReturnErrorMessage_WhenModPathNotProvided()
    {
        // Arrange
        var expectedErrorMessage = "❌ MODS FOLDER PATH NOT PROVIDED!";

        _mockYamlSettingsCache.Setup(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"))
            .Returns((DirectoryInfo)null);

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing"))
            .Returns(expectedErrorMessage);

        // Act
        var result = await _service.ScanModsUnpackedAsync();

        // Assert
        Assert.Equal(expectedErrorMessage, result);
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_ShouldReportProgress()
    {
        // Arrange
        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        _mockYamlSettingsCache.Setup(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"))
            .Returns((DirectoryInfo)null);

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing"))
            .Returns("Error message");

        // Act
        await _service.ScanModsUnpackedAsync(progress);

        // Assert
        Assert.Contains(progressReports, p => p.PercentComplete == 0);
        Assert.Contains(progressReports, p => p.CurrentOperation.Contains("Starting"));
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_ShouldRespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ScanModsUnpackedAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ScanModsArchivedAsync_ShouldReturnErrorMessage_WhenModPathNotProvided()
    {
        // Arrange
        var expectedErrorMessage = "❌ MODS FOLDER PATH NOT PROVIDED!";

        _mockYamlSettingsCache.Setup(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"))
            .Returns((DirectoryInfo)null);

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing"))
            .Returns(expectedErrorMessage);

        // Act
        var result = await _service.ScanModsArchivedAsync();

        // Assert
        Assert.Equal(expectedErrorMessage, result);
    }

    [Fact]
    public async Task GetModsCombinedResultAsync_ShouldReturnErrorMessage_WhenModPathNotProvided()
    {
        // Arrange
        var expectedErrorMessage = "❌ MODS FOLDER PATH NOT PROVIDED!";

        _mockYamlSettingsCache.Setup(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"))
            .Returns((DirectoryInfo)null);

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing"))
            .Returns(expectedErrorMessage);

        // Act
        var result = await _service.GetModsCombinedResultAsync();

        // Assert
        Assert.Equal(expectedErrorMessage, result);
    }

    [Fact]
    public async Task GetModsCombinedResultAsync_ShouldReportProgress()
    {
        // Arrange
        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        _mockYamlSettingsCache.Setup(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"))
            .Returns((DirectoryInfo)null);

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing"))
            .Returns("Error message");

        // Act
        await _service.GetModsCombinedResultAsync(progress);

        // Assert
        Assert.Contains(progressReports, p => p.PercentComplete == 0);
        Assert.Contains(progressReports, p => p.CurrentOperation.Contains("Starting"));
    }

    [Fact]
    public void ClearCache_ShouldClearCachedResults()
    {
        // Arrange - First call to cache a result
        _mockYamlSettingsCache.Setup(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"))
            .Returns((DirectoryInfo)null);

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing"))
            .Returns("Error message");

        // First call to cache the result
        var firstResult = _service.ScanModsUnpackedAsync().Result;

        // Act
        _service.ClearCache();

        // Assert - Verify that the service makes a new call to the settings service
        _mockYamlSettingsCache.Verify(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"), Times.Once);

        // Second call should make another call to the settings service
        var secondResult = _service.ScanModsUnpackedAsync().Result;
        _mockYamlSettingsCache.Verify(m => m.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path"),
            Times.Exactly(2));
    }
}