using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Scanner111.Core.Configuration;
using Scanner111.Core.DependencyInjection;
using Scanner111.Core.IO;

namespace Scanner111.Test.Configuration;

public class AsyncYamlSettingsCoreTests : IAsyncLifetime
{
    private readonly Mock<IFileIoCore> _mockFileIo;
    private readonly Mock<ILogger<AsyncYamlSettingsCore>> _mockLogger;
    private readonly YamlSettingsOptions _options;
    private IAsyncYamlSettingsCore _sut = null!; // Will be initialized in InitializeAsync
    private string _testDirectory;
    
    public AsyncYamlSettingsCoreTests()
    {
        _mockFileIo = new Mock<IFileIoCore>();
        _mockLogger = new Mock<ILogger<AsyncYamlSettingsCore>>();
        _options = new YamlSettingsOptions
        {
            CacheTtl = TimeSpan.FromSeconds(1),
            EnableMetrics = true
        };
        _testDirectory = Path.Combine(Path.GetTempPath(), $"yaml_test_{Guid.NewGuid()}");
    }
    
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_testDirectory);
        _sut = new AsyncYamlSettingsCore(
            _mockFileIo.Object,
            _mockLogger.Object,
            Options.Create(_options));
        await Task.CompletedTask;
    }
    
    public async Task DisposeAsync()
    {
        await _sut.DisposeAsync();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task GetPathForStoreAsync_ShouldReturnCorrectPaths()
    {
        // Act
        var mainPath = await _sut.GetPathForStoreAsync(YamlStore.Main);
        var settingsPath = await _sut.GetPathForStoreAsync(YamlStore.Settings);
        var ignorePath = await _sut.GetPathForStoreAsync(YamlStore.Ignore);
        var gamePath = await _sut.GetPathForStoreAsync(YamlStore.Game);
        var testPath = await _sut.GetPathForStoreAsync(YamlStore.Test);
        
        // Assert
        mainPath.Should().Be(Path.Combine("CLASSIC Data", "databases", "CLASSIC Main.yaml"));
        settingsPath.Should().Be("CLASSIC Settings.yaml");
        ignorePath.Should().Be("CLASSIC Ignore.yaml");
        gamePath.Should().Be(Path.Combine("CLASSIC Data", "databases", "CLASSIC Fallout4.yaml"));
        testPath.Should().Be(Path.Combine("tests", "test_settings.yaml"));
    }
    
    [Fact]
    public async Task GetPathForStoreAsync_ShouldCachePaths()
    {
        // Act
        var path1 = await _sut.GetPathForStoreAsync(YamlStore.Settings);
        var path2 = await _sut.GetPathForStoreAsync(YamlStore.Settings);
        
        // Assert
        path1.Should().Be(path2);
        path1.Should().BeSameAs(path2); // Should be the same string reference
    }
    
    [Fact]
    public async Task LoadYamlAsync_WithValidFile_ShouldReturnParsedData()
    {
        // Arrange
        const string yamlContent = @"
test_settings:
  string_value: test
  bool_value: true
  int_value: 42
  nested:
    deep_value: deep
";
        const string filePath = "test.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        _mockFileIo.Setup(x => x.GetLastWriteTimeAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);
        
        // Act
        var result = await _sut.LoadYamlAsync(filePath);
        
        // Assert
        result.Should().ContainKey("test_settings");
        var settings = result["test_settings"] as Dictionary<string, object?>;
        settings.Should().NotBeNull();
        settings!["string_value"].Should().Be("test");
        settings["bool_value"].Should().Be(true);
        settings["int_value"].Should().Be(42);
    }
    
    [Fact]
    public async Task LoadYamlAsync_WithNonExistentFile_ShouldReturnEmptyDictionary()
    {
        // Arrange
        const string filePath = "nonexistent.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        // Act
        var result = await _sut.LoadYamlAsync(filePath);
        
        // Assert
        result.Should().BeEmpty();
    }
    
    [Fact]
    public async Task LoadYamlAsync_ShouldCacheStaticFiles()
    {
        // Arrange
        const string yamlContent = @"
test_data:
  value: cached
";
        var mainPath = Path.Combine("CLASSIC Data", "databases", "CLASSIC Main.yaml");
        
        _mockFileIo.Setup(x => x.FileExistsAsync(mainPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(mainPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        
        // Act
        var result1 = await _sut.LoadYamlAsync(mainPath);
        var result2 = await _sut.LoadYamlAsync(mainPath);
        
        // Assert
        result1.Should().BeSameAs(result2); // Should be the same object reference
        _mockFileIo.Verify(x => x.ReadFileAsync(mainPath, It.IsAny<CancellationToken>()), 
            Times.Once); // Should only read once
    }
    
    [Fact]
    public async Task GetSettingAsync_WithValidPath_ShouldReturnValue()
    {
        // Arrange
        const string yamlContent = @"
CLASSIC_Settings:
  Managed_Game: Fallout 4
  Max_Lines: 1000
  Enable_Feature: true
";
        var settingsPath = "CLASSIC Settings.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        
        // Act
        var stringValue = await _sut.GetSettingAsync<string>(
            YamlStore.Settings, "CLASSIC_Settings.Managed_Game");
        var intValue = await _sut.GetSettingAsync<int>(
            YamlStore.Settings, "CLASSIC_Settings.Max_Lines");
        var boolValue = await _sut.GetSettingAsync<bool>(
            YamlStore.Settings, "CLASSIC_Settings.Enable_Feature");
        
        // Assert
        stringValue.Should().Be("Fallout 4");
        intValue.Should().Be(1000);
        boolValue.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetSettingAsync_WithInvalidPath_ShouldReturnDefault()
    {
        // Arrange
        const string yamlContent = @"
CLASSIC_Settings:
  Test: value
";
        var settingsPath = "CLASSIC Settings.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        
        // Act
        var result = await _sut.GetSettingAsync<string>(
            YamlStore.Settings, "CLASSIC_Settings.NonExistent");
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetSettingAsync_WithNewValue_ShouldUpdateFile()
    {
        // Arrange
        const string initialContent = @"
CLASSIC_Settings:
  Test: old_value
";
        var settingsPath = "CLASSIC Settings.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialContent);
        
        string? savedContent = null;
        _mockFileIo.Setup(x => x.WriteFileAsync(
                settingsPath, 
                It.IsAny<string>(), 
                It.IsAny<Encoding>(), 
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Encoding?, CancellationToken>((_, content, _, _) => savedContent = content)
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _sut.GetSettingAsync(
            YamlStore.Settings, "CLASSIC_Settings.Test", "new_value");
        
        // Assert
        result.Should().Be("new_value");
        savedContent.Should().NotBeNullOrEmpty();
        savedContent.Should().Contain("new_value");
        _mockFileIo.Verify(x => x.WriteFileAsync(
            settingsPath, 
            It.IsAny<string>(), 
            It.IsAny<Encoding>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
    
    [Fact]
    public async Task GetSettingAsync_ModifyingStaticStore_ShouldThrow()
    {
        // Arrange
        const string yamlContent = @"
test_data:
  value: static
";
        var mainPath = Path.Combine("CLASSIC Data", "databases", "CLASSIC Main.yaml");
        
        _mockFileIo.Setup(x => x.FileExistsAsync(mainPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(mainPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _sut.GetSettingAsync(YamlStore.Main, "test_data.value", "new_value"));
    }
    
    [Fact]
    public async Task LoadMultipleStoresAsync_ShouldLoadAllConcurrently()
    {
        // Arrange
        const string mainContent = @"main_data: value1";
        const string settingsContent = @"settings_data: value2";
        
        var mainPath = Path.Combine("CLASSIC Data", "databases", "CLASSIC Main.yaml");
        var settingsPath = "CLASSIC Settings.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(mainPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainContent);
        _mockFileIo.Setup(x => x.ReadFileAsync(settingsPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settingsContent);
        
        // Act
        var stores = new[] { YamlStore.Main, YamlStore.Settings };
        var results = await _sut.LoadMultipleStoresAsync(stores);
        
        // Assert
        results.Should().HaveCount(2);
        results[YamlStore.Main].Should().ContainKey("main_data");
        results[YamlStore.Settings].Should().ContainKey("settings_data");
    }
    
    [Fact]
    public async Task GetMetrics_ShouldReturnPerformanceMetrics()
    {
        // Arrange
        const string yamlContent = @"test: value";
        const string filePath = "test.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        _mockFileIo.Setup(x => x.GetLastWriteTimeAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);
        
        // Act
        await _sut.LoadYamlAsync(filePath);
        await _sut.LoadYamlAsync(filePath); // Cache hit
        var metrics = _sut.GetMetrics();
        
        // Assert
        metrics.Should().ContainKey("CacheHits");
        metrics.Should().ContainKey("CacheMisses");
        metrics.Should().ContainKey("FileReads");
        metrics["CacheHits"].Should().BeGreaterThan(0);
        metrics["FileReads"].Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task ClearCache_ShouldRemoveAllCachedData()
    {
        // Arrange
        const string yamlContent = @"test: value";
        const string filePath = "test.yaml";
        
        _mockFileIo.Setup(x => x.FileExistsAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockFileIo.Setup(x => x.ReadFileAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(yamlContent);
        _mockFileIo.Setup(x => x.GetLastWriteTimeAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);
        
        await _sut.LoadYamlAsync(filePath);
        
        // Act
        _sut.ClearCache();
        await _sut.LoadYamlAsync(filePath); // Should read again
        
        // Assert
        _mockFileIo.Verify(x => x.ReadFileAsync(filePath, It.IsAny<CancellationToken>()), 
            Times.Exactly(2));
    }
    
    [Fact]
    public async Task DependencyInjection_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddYamlSettings(options =>
        {
            options.CacheTtl = TimeSpan.FromSeconds(10);
            options.DefaultGame = "Skyrim";
        });
        
        // Act - Build provider asynchronously to maintain async context
        var provider = await Task.Run(() => services.BuildServiceProvider());
        var asyncCore = provider.GetService<IAsyncYamlSettingsCore>();
        var syncCache = provider.GetService<IYamlSettingsCache>();
        var fileIo = provider.GetService<IFileIoCore>();
        
        // Assert
        asyncCore.Should().NotBeNull();
        syncCache.Should().NotBeNull();
        fileIo.Should().NotBeNull();
        
        // Cleanup - Use async disposal for IAsyncDisposable services
        if (provider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}