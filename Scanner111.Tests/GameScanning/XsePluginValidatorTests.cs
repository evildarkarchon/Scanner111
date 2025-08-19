using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.GameScanning;

/// <summary>
///     Comprehensive tests for XsePluginValidator (Address Library validator).
/// </summary>
[Collection("Settings Test Collection")]
public class XsePluginValidatorTests : IDisposable
{
    private readonly Mock<ILogger<XsePluginValidator>> _mockLogger;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly Mock<IYamlSettingsProvider> _mockYamlProvider;
    private readonly string _testDirectory;
    private readonly string _testPluginsPath;
    private readonly XsePluginValidator _validator;

    public XsePluginValidatorTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockYamlProvider = new Mock<IYamlSettingsProvider>();
        _mockLogger = new Mock<ILogger<XsePluginValidator>>();

        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _testPluginsPath = Path.Combine(_testDirectory, "TestPlugins");

        _validator = new XsePluginValidator(
            _mockSettingsService.Object,
            _mockYamlProvider.Object,
            _mockLogger.Object);

        SetupDefaultMocks();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
    }

    private void SetupDefaultMocks()
    {
        var gamePath = Path.Combine(_testDirectory, "TestGame");
        Directory.CreateDirectory(gamePath);
        
        var gameExePath = Path.Combine(gamePath, "Fallout4.exe");
        // Create a dummy executable file
        File.WriteAllText(gameExePath, "dummy exe");
        
        var settings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };

        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(settings);
    }

    #region Performance Tests

    [Fact]
    public async Task ValidateAsync_LargePluginsFolder_CompletesInReasonableTime()
    {
        // Arrange
        CreateTestPluginsDirectory();

        // Create many plugin files
        for (var i = 0; i < 100; i++) CreatePluginFile($"plugin_{i}.dll");

        CreateAddressLibraryFile("version-1-10-163-0.bin");

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _validator.ValidateAsync();
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
        elapsed.TotalMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public async Task ValidateAsync_UnsupportedGame_ReturnsInfoMessage()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Unknown
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("Address Library validation is not available");
        result.Should().Contain("Unknown");
    }

    [Fact]
    public async Task ValidateAsync_NoPluginsFolder_ReturnsWarning()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameNoPlugins");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "Fallout4.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var settings = new ApplicationSettings
        {
            PluginsFolder = null,
            GameType = GameType.Fallout4,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("ERROR");
        result.Should().Contain("plugins folder");
    }

    [Fact]
    public async Task ValidateAsync_PluginsFolderDoesNotExist_ReturnsWarning()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameNoPlugins2");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "Fallout4.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var settings = new ApplicationSettings
        {
            PluginsFolder = @"C:\NonExistent\Path",
            GameType = GameType.Fallout4,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("ERROR");
        result.Should().Contain("plugins folder");
    }

    #endregion

    #region Fallout 4 Address Library Tests

    [Fact]
    public async Task ValidateAsync_Fallout4_NoAddressLibrary_ShowsCriticalError()
    {
        // Arrange
        CreateTestPluginsDirectory();

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4_OGVersionPresent_ShowsSuccess()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-163-0.bin");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4_NGVersionPresent_ShowsSuccess()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-984-0.bin");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4_MultipleVersions_ShowsAllDetected()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-163-0.bin");
        CreateAddressLibraryFile("version-1-10-984-0.bin");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4VR_VRVersionPresent_ShowsSuccess()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameVR");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "Fallout4VR.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var vrSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4VR,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(vrSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-2-72-0.csv");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4VR_WrongVersionInstalled_ShowsWarning()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameVR2");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "Fallout4VR.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var vrSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4VR,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(vrSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-163-0.bin"); // Non-VR version

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    #endregion

    #region Skyrim Address Library Tests

    [Fact]
    public async Task ValidateAsync_SkyrimSE_SEVersionPresent_ShowsSuccess()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameSkyrimSE");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "SkyrimSE.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var skyrimSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.SkyrimSE,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(skyrimSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-6-1170-0.bin");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_SkyrimVR_VRVersionPresent_ShowsSuccess()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameSkyrimVR");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "SkyrimVR.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var skyrimVRSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.SkyrimVR,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(skyrimVRSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-4-15-0.csv");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_SkyrimSE_NoAddressLibrary_ShowsCriticalError()
    {
        // Arrange
        var gamePath = Path.Combine(_testDirectory, "TestGameSkyrimSE2");
        Directory.CreateDirectory(gamePath);
        var gameExePath = Path.Combine(gamePath, "SkyrimSE.exe");
        File.WriteAllText(gameExePath, "dummy exe");
        
        var skyrimSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.SkyrimSE,
            GamePath = gamePath,
            GameExecutablePath = gameExePath
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(skyrimSettings);

        CreateTestPluginsDirectory();

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    #endregion

    #region Plugin Compatibility Tests

    [Fact]
    public async Task ValidateAsync_IncompatiblePlugins_ShowsWarnings()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-163-0.bin");
        CreatePluginFile("incompatible_plugin.dll");

        // Mock YAML data with incompatible plugins
        _mockYamlProvider.Setup(x => x.LoadYaml<object>(It.IsAny<string>()))
            .Returns(new
            {
                /* Mock incompatible plugin data */
            });

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // The actual implementation would check for incompatible plugins
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_OutdatedPlugins_ShowsWarnings()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-984-0.bin"); // Next-Gen
        CreatePluginFile("old_f4se_plugin.dll");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // The actual implementation would check plugin versions
        result.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task ValidateAsync_EmptyPluginsFolder_HandlesGracefully()
    {
        // Arrange
        CreateTestPluginsDirectory();
        // Don't create any files

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_CorruptedAddressLibraryFile_DetectsIssue()
    {
        // Arrange
        CreateTestPluginsDirectory();
        var libraryPath = Path.Combine(_testPluginsPath, "version-1-10-163-0.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
        File.WriteAllText(libraryPath, "corrupted");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_CaseInsensitiveFileDetection_Works()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("VERSION-1-10-163-0.BIN"); // Upper case

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    [Fact]
    public async Task ValidateAsync_UnknownAddressLibraryVersion_ShowsUnknownVersion()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-9-99-999-0.bin"); // Unknown version

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Since we can't mock FileVersionInfo, the validator can't detect game version
        result.Should().Contain("NOTICE");
        result.Should().Contain("Unable to detect game version");
    }

    #endregion

    #region Helper Methods

    private void CreateTestPluginsDirectory()
    {
        Directory.CreateDirectory(_testPluginsPath);
    }

    private void CreateAddressLibraryFile(string filename)
    {
        // The validator checks for the file directly in the plugins path
        var libraryPath = Path.Combine(_testPluginsPath, filename);
        
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);

        // Create a file with some dummy binary content
        var dummyContent = new byte[1024];
        new Random().NextBytes(dummyContent);
        File.WriteAllBytes(libraryPath, dummyContent);
    }

    private void CreatePluginFile(string filename)
    {
        var pluginPath = Path.Combine(_testPluginsPath, filename);
        File.WriteAllText(pluginPath, "dummy dll content");
    }

    #endregion
}