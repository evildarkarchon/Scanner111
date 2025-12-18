using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Settings;

namespace Scanner111.Common.Tests.Services.Settings;

public class UserSettingsServiceTests : IDisposable
{
    private readonly string _testSettingsPath;
    private readonly UserSettingsService _service;

    public UserSettingsServiceTests()
    {
        // Use a unique temp file for each test
        _testSettingsPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"Scanner111_Test_{Guid.NewGuid()}.json");
        _service = new UserSettingsService(NullLogger<UserSettingsService>.Instance, _testSettingsPath);
    }

    public void Dispose()
    {
        // Clean up test file
        if (File.Exists(_testSettingsPath))
        {
            File.Delete(_testSettingsPath);
        }
        GC.SuppressFinalize(this);
    }

    #region LoadAsync Tests

    [Fact]
    public async Task LoadAsync_WithNoFile_ReturnsDefaults()
    {
        // Act
        var settings = await _service.LoadAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.SelectedGame.Should().Be("Fallout4");
        settings.IsVrMode.Should().BeFalse();
        settings.CustomScanPath.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedFile_ReturnsDefaults()
    {
        // Arrange
        await File.WriteAllTextAsync(_testSettingsPath, "{ invalid json }}}");

        // Act
        var settings = await _service.LoadAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.SelectedGame.Should().Be("Fallout4");
    }

    #endregion

    #region SaveAsync and Round-trip Tests

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTrips()
    {
        // Arrange
        var originalSettings = new UserSettings
        {
            CustomScanPath = @"C:\Test\Path",
            ModsFolderPath = @"C:\Test\Mods",
            IniFolderPath = @"C:\Test\INI",
            GameRootPath = @"C:\Games\Fallout4",
            DocumentsPath = @"C:\Users\Test\Documents\My Games\Fallout4",
            SelectedGame = "SkyrimSE",
            IsVrMode = true
        };

        // Act
        await _service.SaveAsync(originalSettings);
        _service.ClearCache(); // Force reload from disk
        var loadedSettings = await _service.LoadAsync();

        // Assert
        loadedSettings.CustomScanPath.Should().Be(originalSettings.CustomScanPath);
        loadedSettings.ModsFolderPath.Should().Be(originalSettings.ModsFolderPath);
        loadedSettings.IniFolderPath.Should().Be(originalSettings.IniFolderPath);
        loadedSettings.GameRootPath.Should().Be(originalSettings.GameRootPath);
        loadedSettings.DocumentsPath.Should().Be(originalSettings.DocumentsPath);
        loadedSettings.SelectedGame.Should().Be(originalSettings.SelectedGame);
        loadedSettings.IsVrMode.Should().Be(originalSettings.IsVrMode);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var nestedPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"Scanner111_Test_{Guid.NewGuid()}",
            "nested",
            "settings.json");
        var nestedService = new UserSettingsService(NullLogger<UserSettingsService>.Instance, nestedPath);
        var settings = UserSettings.Default;

        try
        {
            // Act
            await nestedService.SaveAsync(settings);

            // Assert
            File.Exists(nestedPath).Should().BeTrue();
        }
        finally
        {
            // Clean up
            var dir = System.IO.Path.GetDirectoryName(nestedPath);
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(nestedPath))!, true);
            }
        }
    }

    #endregion

    #region GetCurrentAsync Tests

    [Fact]
    public async Task GetCurrentAsync_ReturnsCachedValue()
    {
        // Arrange
        var settings = new UserSettings { CustomScanPath = @"C:\Test" };
        await _service.SaveAsync(settings);

        // Act
        var first = await _service.GetCurrentAsync();
        var second = await _service.GetCurrentAsync();

        // Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task GetCurrentAsync_AfterClearCache_ReloadsFromDisk()
    {
        // Arrange
        var settings = new UserSettings { CustomScanPath = @"C:\Test" };
        await _service.SaveAsync(settings);
        var first = await _service.GetCurrentAsync();

        // Act
        _service.ClearCache();
        var second = await _service.GetCurrentAsync();

        // Assert
        first.Should().NotBeSameAs(second);
        first.CustomScanPath.Should().Be(second.CustomScanPath);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ModifiesAndSavesSettings()
    {
        // Arrange
        var initialSettings = new UserSettings { CustomScanPath = @"C:\Initial" };
        await _service.SaveAsync(initialSettings);

        // Act
        var updated = await _service.UpdateAsync(s => s with { CustomScanPath = @"C:\Updated" });

        // Assert
        updated.CustomScanPath.Should().Be(@"C:\Updated");

        // Verify persisted
        _service.ClearCache();
        var reloaded = await _service.GetCurrentAsync();
        reloaded.CustomScanPath.Should().Be(@"C:\Updated");
    }

    #endregion

    #region Convenience Method Tests

    [Fact]
    public async Task SetCustomScanPathAsync_UpdatesSettings()
    {
        // Act
        await _service.SetCustomScanPathAsync(@"C:\Custom\Scan");
        var result = await _service.GetCustomScanPathAsync();

        // Assert
        result.Should().Be(@"C:\Custom\Scan");
    }

    [Fact]
    public async Task SetModsFolderPathAsync_UpdatesSettings()
    {
        // Act
        await _service.SetModsFolderPathAsync(@"C:\Mods");
        var result = await _service.GetModsFolderPathAsync();

        // Assert
        result.Should().Be(@"C:\Mods");
    }

    [Fact]
    public async Task SetIniFolderPathAsync_UpdatesSettings()
    {
        // Act
        await _service.SetIniFolderPathAsync(@"C:\INI");
        var result = await _service.GetIniFolderPathAsync();

        // Assert
        result.Should().Be(@"C:\INI");
    }

    [Fact]
    public async Task SetGameRootPathAsync_UpdatesSettings()
    {
        // Act
        await _service.SetGameRootPathAsync(@"C:\Games\Fallout4");
        var result = await _service.GetGameRootPathAsync();

        // Assert
        result.Should().Be(@"C:\Games\Fallout4");
    }

    [Fact]
    public async Task SetDocumentsPathAsync_UpdatesSettings()
    {
        // Act
        await _service.SetDocumentsPathAsync(@"C:\Docs\Fallout4");
        var result = await _service.GetDocumentsPathAsync();

        // Assert
        result.Should().Be(@"C:\Docs\Fallout4");
    }

    [Fact]
    public async Task SetSelectedGameAsync_UpdatesSettings()
    {
        // Act
        await _service.SetSelectedGameAsync("SkyrimSE");
        var result = await _service.GetSelectedGameAsync();

        // Assert
        result.Should().Be("SkyrimSE");
    }

    [Fact]
    public async Task GetSelectedGameAsync_WithNoSetting_ReturnsFallout4()
    {
        // Act
        var result = await _service.GetSelectedGameAsync();

        // Assert
        result.Should().Be("Fallout4");
    }

    [Fact]
    public async Task SetIsVrModeAsync_UpdatesSettings()
    {
        // Act
        await _service.SetIsVrModeAsync(true);
        var result = await _service.GetIsVrModeAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetCustomScanPathAsync_ToNull_ClearsPath()
    {
        // Arrange
        await _service.SetCustomScanPathAsync(@"C:\Existing");

        // Act
        await _service.SetCustomScanPathAsync(null);
        var result = await _service.GetCustomScanPathAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
