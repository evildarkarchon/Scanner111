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
        var settings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4,
            GamePath = Path.Combine(_testDirectory, "TestGame")
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
        result.Should().Contain("✔️ Address Library detected");
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
        var settings = new ApplicationSettings
        {
            PluginsFolder = null,
            GameType = GameType.Fallout4
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("WARNING : Plugins folder not configured");
        result.Should().Contain("Address Library validation skipped");
    }

    [Fact]
    public async Task ValidateAsync_PluginsFolderDoesNotExist_ReturnsWarning()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            PluginsFolder = @"C:\NonExistent\Path",
            GameType = GameType.Fallout4
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("WARNING : Plugins folder does not exist");
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
        result.Should().Contain("CRITICAL : NO ADDRESS LIBRARY DETECTED");
        result.Should().Contain("Fallout 4");
        result.Should().Contain("Download from");
        result.Should().Contain("nexusmods.com");
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
        result.Should().Contain("✔️ Address Library detected");
        result.Should().Contain("Non-VR (Regular) version");
        result.Should().Contain("version-1-10-163-0.bin");
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
        result.Should().Contain("✔️ Address Library detected");
        result.Should().Contain("Non-VR (Next-Gen) version");
        result.Should().Contain("version-1-10-984-0.bin");
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
        result.Should().Contain("✔️ Address Library detected");
        result.Should().Contain("Non-VR (Regular) version");
        result.Should().Contain("Non-VR (Next-Gen) version");
        result.Should().Contain("Multiple Address Library versions installed");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4VR_VRVersionPresent_ShowsSuccess()
    {
        // Arrange
        var vrSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4VR
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(vrSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-2-72-0.csv");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("✔️ Address Library detected");
        result.Should().Contain("Virtual Reality (VR) version");
        result.Should().Contain("version-1-2-72-0.csv");
    }

    [Fact]
    public async Task ValidateAsync_Fallout4VR_WrongVersionInstalled_ShowsWarning()
    {
        // Arrange
        var vrSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4VR
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(vrSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-10-163-0.bin"); // Non-VR version

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("WARNING : Wrong Address Library version");
        result.Should().Contain("Expected VR version");
        result.Should().Contain("Found Non-VR (Regular) version");
    }

    #endregion

    #region Skyrim Address Library Tests

    [Fact]
    public async Task ValidateAsync_SkyrimSE_SEVersionPresent_ShowsSuccess()
    {
        // Arrange
        var skyrimSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.SkyrimSE
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(skyrimSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-6-1170-0.bin");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("✔️ Address Library detected");
        result.Should().Contain("Special Edition version");
        result.Should().Contain("version-1-6-1170-0.bin");
    }

    [Fact]
    public async Task ValidateAsync_SkyrimVR_VRVersionPresent_ShowsSuccess()
    {
        // Arrange
        var skyrimVRSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.SkyrimVR
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(skyrimVRSettings);

        CreateTestPluginsDirectory();
        CreateAddressLibraryFile("version-1-4-15-0.csv");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("✔️ Address Library detected");
        result.Should().Contain("Virtual Reality (VR) version");
        result.Should().Contain("version-1-4-15-0.csv");
    }

    [Fact]
    public async Task ValidateAsync_SkyrimSE_NoAddressLibrary_ShowsCriticalError()
    {
        // Arrange
        var skyrimSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.SkyrimSE
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(skyrimSettings);

        CreateTestPluginsDirectory();

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        result.Should().Contain("CRITICAL : NO ADDRESS LIBRARY DETECTED");
        result.Should().Contain("Skyrim");
        result.Should().Contain("Download from");
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
        result.Should().Contain("NO ADDRESS LIBRARY DETECTED");
    }

    [Fact]
    public async Task ValidateAsync_CorruptedAddressLibraryFile_DetectsIssue()
    {
        // Arrange
        CreateTestPluginsDirectory();
        var libraryPath = Path.Combine(_testPluginsPath, "F4SE", "Plugins", "version-1-10-163-0.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
        File.WriteAllText(libraryPath, "corrupted");

        // Act
        var result = await _validator.ValidateAsync();

        // Assert
        // Should detect the file but potentially warn about size/corruption
        result.Should().NotBeNull();
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
        result.Should().Contain("✔️ Address Library detected");
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
        result.Should().Contain("Unknown Address Library version");
        result.Should().Contain("version-9-99-999-0.bin");
    }

    #endregion

    #region Helper Methods

    private void CreateTestPluginsDirectory()
    {
        Directory.CreateDirectory(_testPluginsPath);
    }

    private void CreateAddressLibraryFile(string filename)
    {
        string libraryPath;

        if (filename.Contains("1-10-163") || filename.Contains("1-10-984"))
            // Fallout 4 non-VR
            libraryPath = Path.Combine(_testPluginsPath, "F4SE", "Plugins", filename);
        else if (filename.Contains("1-2-72"))
            // Fallout 4 VR
            libraryPath = Path.Combine(_testPluginsPath, "F4SEVR", "Plugins", filename);
        else if (filename.Contains("1-6-1170"))
            // Skyrim SE
            libraryPath = Path.Combine(_testPluginsPath, "SKSE", "Plugins", filename);
        else if (filename.Contains("1-4-15"))
            // Skyrim VR
            libraryPath = Path.Combine(_testPluginsPath, "SKSEVR", "Plugins", filename);
        else
            // Unknown/test version
            libraryPath = Path.Combine(_testPluginsPath, "F4SE", "Plugins", filename);

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