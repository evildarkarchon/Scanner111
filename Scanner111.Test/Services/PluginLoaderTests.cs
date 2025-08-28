using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using System.Text;

namespace Scanner111.Test.Services;

/// <summary>
///     Unit tests for the PluginLoader service.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Service")]
public class PluginLoaderTests : IDisposable
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly PluginLoader _pluginLoader;
    private readonly string _testDirectory;
    private readonly string _testLoadOrderPath;

    public PluginLoaderTests()
    {
        _logger = Substitute.For<ILogger<PluginLoader>>();
        _pluginLoader = new PluginLoader(_logger);

        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PluginLoaderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testLoadOrderPath = Path.Combine(_testDirectory, "loadorder.txt");
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task LoadFromLoadOrderFileAsync_FileNotExists_ReturnsNotFoundResult()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var (plugins, pluginsLoaded, fragment) = await _pluginLoader.LoadFromLoadOrderFileAsync(nonExistentPath);

        // Assert
        plugins.Should().BeEmpty();
        pluginsLoaded.Should().BeFalse();
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("No loadorder.txt file found");
        fragment.Type.Should().Be(FragmentType.Info);
    }

    [Fact]
    public async Task LoadFromLoadOrderFileAsync_ValidFile_LoadsPluginsSuccessfully()
    {
        // Arrange
        var loadOrderContent = new StringBuilder()
            .AppendLine("# Skyrim.esm")
            .AppendLine("Skyrim.esm")
            .AppendLine("Update.esm")
            .AppendLine("Dawnguard.esm")
            .AppendLine("MyMod.esp")
            .AppendLine("")
            .AppendLine("AnotherMod.esp")
            .ToString();

        await File.WriteAllTextAsync(_testLoadOrderPath, loadOrderContent);

        // Act
        var (plugins, pluginsLoaded, fragment) = await _pluginLoader.LoadFromLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        plugins.Should().HaveCount(5);
        plugins.Should().ContainKeys("Skyrim.esm", "Update.esm", "Dawnguard.esm", "MyMod.esp", "AnotherMod.esp");
        plugins.Values.Should().AllBe("LO");
        pluginsLoaded.Should().BeTrue();
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("LOADORDER.TXT FILE FOUND");
        fragment.Type.Should().Be(FragmentType.Info);
    }

    [Fact]
    public async Task LoadFromLoadOrderFileAsync_EmptyFile_ReturnsEmptyResult()
    {
        // Arrange
        await File.WriteAllTextAsync(_testLoadOrderPath, "# Header only\n");

        // Act
        var (plugins, pluginsLoaded, fragment) = await _pluginLoader.LoadFromLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        plugins.Should().BeEmpty();
        pluginsLoaded.Should().BeFalse();
        fragment.Should().NotBeNull();
        fragment.Content.Should().Contain("LOADORDER.TXT FILE FOUND");
    }

    [Fact]
    public async Task LoadFromLoadOrderFileAsync_DuplicateEntries_IgnoresDuplicates()
    {
        // Arrange
        var loadOrderContent = new StringBuilder()
            .AppendLine("# Header")
            .AppendLine("Skyrim.esm")
            .AppendLine("Skyrim.esm")
            .AppendLine("Update.esm")
            .AppendLine("Update.esm")
            .ToString();

        await File.WriteAllTextAsync(_testLoadOrderPath, loadOrderContent);

        // Act
        var (plugins, pluginsLoaded, fragment) = await _pluginLoader.LoadFromLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        plugins.Should().HaveCount(2);
        plugins.Should().ContainKeys("Skyrim.esm", "Update.esm");
        pluginsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFromLoadOrderFileAsync_UnreadableFile_ReturnsErrorResult()
    {
        // Arrange
        await File.WriteAllTextAsync(_testLoadOrderPath, "test content");
        
        // Make file unreadable by opening it with exclusive access
        using var fileStream = new FileStream(_testLoadOrderPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var (plugins, pluginsLoaded, fragment) = await _pluginLoader.LoadFromLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        plugins.Should().BeEmpty();
        pluginsLoaded.Should().BeFalse();
        fragment.Should().NotBeNull();
        fragment.Type.Should().Be(FragmentType.Warning);
        fragment.Content.Should().Contain("Error reading loadorder.txt");
    }

    [Fact]
    public void ScanPluginsFromLog_EmptyInput_ReturnsEmptyResult()
    {
        // Arrange
        var emptySegment = new List<string>();
        var gameVersion = new Version(1, 0, 0);
        var currentVersion = new Version(1, 37, 0);

        // Act
        var (plugins, limitTriggered, limitCheckDisabled) = 
            _pluginLoader.ScanPluginsFromLog(emptySegment, gameVersion, currentVersion);

        // Assert
        plugins.Should().BeEmpty();
        limitTriggered.Should().BeFalse();
        limitCheckDisabled.Should().BeFalse();
    }

    [Fact]
    public void ScanPluginsFromLog_ValidPluginEntries_ExtractsPluginData()
    {
        // Arrange
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[01] Update.esm",
            "[FE:000] MyMod.esl",
            "[02] TestMod.esp",
            "[03] SomeDLL.dll",
            "Invalid line without bracket",
            "[04] AnotherMod.esp"
        };
        var gameVersion = new Version(1, 0, 0);
        var currentVersion = new Version(1, 37, 0);

        // Act
        var (plugins, limitTriggered, limitCheckDisabled) = 
            _pluginLoader.ScanPluginsFromLog(pluginSegment, gameVersion, currentVersion);

        // Assert
        plugins.Should().HaveCount(6);
        plugins.Should().ContainKey("Skyrim.esm").WhoseValue.Should().Be("00");
        plugins.Should().ContainKey("Update.esm").WhoseValue.Should().Be("01");
        plugins.Should().ContainKey("MyMod.esl").WhoseValue.Should().Be("FE000");
        plugins.Should().ContainKey("TestMod.esp").WhoseValue.Should().Be("02");
        plugins.Should().ContainKey("SomeDLL.dll").WhoseValue.Should().Be("DLL");
        plugins.Should().ContainKey("AnotherMod.esp").WhoseValue.Should().Be("04");
        limitTriggered.Should().BeFalse();
        limitCheckDisabled.Should().BeFalse();
    }

    [Fact]
    public void ScanPluginsFromLog_PluginLimitMarker_TriggersLimit()
    {
        // Arrange
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[FF] PluginLimit.esp",
            "[01] TestMod.esp"
        };
        var gameVersion = new Version(1, 0, 0); // Original game version
        var currentVersion = new Version(1, 37, 0);

        // Act
        var (plugins, limitTriggered, limitCheckDisabled) = 
            _pluginLoader.ScanPluginsFromLog(pluginSegment, gameVersion, currentVersion);

        // Assert
        plugins.Should().HaveCount(3);
        limitTriggered.Should().BeTrue();
        limitCheckDisabled.Should().BeFalse();
    }

    [Fact]
    public void ScanPluginsFromLog_NewGameVersionPre137_DisablesLimitCheck()
    {
        // Arrange
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[FF] PluginLimit.esp"
        };
        var gameVersion = new Version(2, 0, 0); // New game version
        var currentVersion = new Version(1, 36, 0); // Pre-1.37

        // Act
        var (plugins, limitTriggered, limitCheckDisabled) = 
            _pluginLoader.ScanPluginsFromLog(pluginSegment, gameVersion, currentVersion);

        // Assert
        plugins.Should().HaveCount(2);
        limitTriggered.Should().BeFalse();
        limitCheckDisabled.Should().BeTrue();
    }

    [Fact]
    public void ScanPluginsFromLog_WithIgnoredPlugins_FiltersCorrectly()
    {
        // Arrange
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[01] IgnoredMod.esp",
            "[02] GoodMod.esp"
        };
        var gameVersion = new Version(1, 0, 0);
        var currentVersion = new Version(1, 37, 0);
        var ignoredPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "IgnoredMod.esp" };

        // Act
        var (plugins, limitTriggered, limitCheckDisabled) = 
            _pluginLoader.ScanPluginsFromLog(pluginSegment, gameVersion, currentVersion, ignoredPlugins);

        // Assert
        plugins.Should().HaveCount(2);
        plugins.Should().ContainKey("Skyrim.esm");
        plugins.Should().ContainKey("GoodMod.esp");
        plugins.Should().NotContainKey("IgnoredMod.esp");
    }

    [Fact]
    public void ScanPluginsFromLog_DuplicatePlugins_IgnoresDuplicates()
    {
        // Arrange
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[00] Skyrim.esm", // Duplicate
            "[01] TestMod.esp"
        };
        var gameVersion = new Version(1, 0, 0);
        var currentVersion = new Version(1, 37, 0);

        // Act
        var (plugins, limitTriggered, limitCheckDisabled) = 
            _pluginLoader.ScanPluginsFromLog(pluginSegment, gameVersion, currentVersion);

        // Assert
        plugins.Should().HaveCount(2);
        plugins.Should().ContainKey("Skyrim.esm").WhoseValue.Should().Be("00");
        plugins.Should().ContainKey("TestMod.esp").WhoseValue.Should().Be("01");
    }

    [Fact]
    public void CreatePluginInfoCollection_LoadOrderOnly_CreatesCorrectCollection()
    {
        // Arrange
        var loadOrderPlugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "LO" },
            { "Update.esm", "LO" },
            { "TestMod.esp", "LO" }
        };

        // Act
        var plugins = _pluginLoader.CreatePluginInfoCollection(loadOrderPlugins: loadOrderPlugins);

        // Assert
        plugins.Should().HaveCount(3);
        plugins.Should().AllSatisfy(p => p.Origin.Should().Be("LO"));
        
        var skyrimPlugin = plugins.First(p => p.Name == "Skyrim.esm");
        skyrimPlugin.Type.Should().Be(PluginType.Master);
        skyrimPlugin.Index.Should().Be(0);

        var testModPlugin = plugins.First(p => p.Name == "TestMod.esp");
        testModPlugin.Type.Should().Be(PluginType.Plugin);
        testModPlugin.Index.Should().Be(2);
    }

    [Fact]
    public void CreatePluginInfoCollection_CrashLogOnly_CreatesCorrectCollection()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" },
            { "TestMod.esp", "01" },
            { "SomeDLL.dll", "DLL" }
        };

        // Act
        var plugins = _pluginLoader.CreatePluginInfoCollection(crashLogPlugins: crashLogPlugins);

        // Assert
        plugins.Should().HaveCount(3);
        
        var skyrimPlugin = plugins.First(p => p.Name == "Skyrim.esm");
        skyrimPlugin.Origin.Should().Be("00");
        skyrimPlugin.Index.Should().BeNull();

        var dllPlugin = plugins.First(p => p.Name == "SomeDLL.dll");
        dllPlugin.Type.Should().Be(PluginType.Dynamic);
        dllPlugin.Origin.Should().Be("DLL");
        dllPlugin.IsDllPlugin.Should().BeTrue();
    }

    [Fact]
    public void CreatePluginInfoCollection_BothSources_PrioritizesLoadOrder()
    {
        // Arrange
        var loadOrderPlugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "LO" },
            { "TestMod.esp", "LO" }
        };
        
        var crashLogPlugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" }, // Should be ignored in favor of load order
            { "AnotherMod.esp", "01" }
        };

        // Act
        var plugins = _pluginLoader.CreatePluginInfoCollection(loadOrderPlugins, crashLogPlugins);

        // Assert
        plugins.Should().HaveCount(3);
        
        var skyrimPlugin = plugins.First(p => p.Name == "Skyrim.esm");
        skyrimPlugin.Origin.Should().Be("LO"); // Load order takes priority
        skyrimPlugin.Index.Should().Be(0);

        var anotherModPlugin = plugins.First(p => p.Name == "AnotherMod.esp");
        anotherModPlugin.Origin.Should().Be("01");
        anotherModPlugin.Index.Should().BeNull();
    }

    [Fact]
    public void CreatePluginInfoCollection_WithIgnoredPlugins_MarksCorrectly()
    {
        // Arrange
        var loadOrderPlugins = new Dictionary<string, string>
        {
            { "GoodMod.esp", "LO" },
            { "BadMod.esp", "LO" }
        };
        
        var ignoredPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BadMod.esp" };

        // Act
        var plugins = _pluginLoader.CreatePluginInfoCollection(loadOrderPlugins, ignoredPlugins: ignoredPlugins);

        // Assert
        plugins.Should().HaveCount(2);
        
        var goodMod = plugins.First(p => p.Name == "GoodMod.esp");
        goodMod.IsIgnored.Should().BeFalse();

        var badMod = plugins.First(p => p.Name == "BadMod.esp");
        badMod.IsIgnored.Should().BeTrue();
    }

    [Fact]
    public void FilterIgnoredPlugins_EmptyIgnoreList_ReturnsAllPlugins()
    {
        // Arrange
        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" },
            { "TestMod.esp", "01" }
        };
        var ignoredPlugins = new HashSet<string>();

        // Act
        var filtered = _pluginLoader.FilterIgnoredPlugins(plugins, ignoredPlugins);

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().BeEquivalentTo(plugins);
    }

    [Fact]
    public void FilterIgnoredPlugins_WithIgnoredPlugins_FiltersCorrectly()
    {
        // Arrange
        var plugins = new Dictionary<string, string>
        {
            { "Skyrim.esm", "00" },
            { "BadMod.esp", "01" },
            { "GoodMod.esp", "02" }
        };
        var ignoredPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "badmod.esp" }; // Case insensitive

        // Act
        var filtered = _pluginLoader.FilterIgnoredPlugins(plugins, ignoredPlugins);

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().ContainKey("Skyrim.esm");
        filtered.Should().ContainKey("GoodMod.esp");
        filtered.Should().NotContainKey("BadMod.esp");
    }

    [Fact]
    public async Task ValidateLoadOrderFileAsync_ValidFile_ReturnsTrue()
    {
        // Arrange
        var loadOrderContent = new StringBuilder()
            .AppendLine("# Header")
            .AppendLine("Skyrim.esm")
            .AppendLine("Update.esm")
            .ToString();

        await File.WriteAllTextAsync(_testLoadOrderPath, loadOrderContent);

        // Act
        var isValid = await _pluginLoader.ValidateLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateLoadOrderFileAsync_EmptyFile_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_testLoadOrderPath, "# Header only");

        // Act
        var isValid = await _pluginLoader.ValidateLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLoadOrderFileAsync_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var isValid = await _pluginLoader.ValidateLoadOrderFileAsync(nonExistentPath);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLoadOrderFileAsync_NoValidPlugins_ReturnsFalse()
    {
        // Arrange
        var loadOrderContent = new StringBuilder()
            .AppendLine("# Header")
            .AppendLine("NotAPlugin.txt")
            .AppendLine("AnotherFile.dat")
            .ToString();

        await File.WriteAllTextAsync(_testLoadOrderPath, loadOrderContent);

        // Act
        var isValid = await _pluginLoader.ValidateLoadOrderFileAsync(_testLoadOrderPath);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatistics_AfterLoadOrder_ReturnsCorrectStats()
    {
        // Arrange
        var loadOrderContent = new StringBuilder()
            .AppendLine("# Header")
            .AppendLine("Skyrim.esm")
            .AppendLine("Update.esm")
            .ToString();

        await File.WriteAllTextAsync(_testLoadOrderPath, loadOrderContent);

        // Act
        await _pluginLoader.LoadFromLoadOrderFileAsync(_testLoadOrderPath);
        var stats = _pluginLoader.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.LoadOrderPluginCount.Should().Be(2);
        stats.CrashLogPluginCount.Should().Be(0);
        stats.LastOperationDuration.Should().BePositive();
        stats.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GetStatistics_AfterScanPlugins_ReturnsCorrectStats()
    {
        // Arrange
        var pluginSegment = new List<string>
        {
            "[00] Skyrim.esm",
            "[01] TestMod.esp"
        };
        var gameVersion = new Version(1, 0, 0);
        var currentVersion = new Version(1, 37, 0);

        // Act
        _pluginLoader.ScanPluginsFromLog(pluginSegment, gameVersion, currentVersion);
        var stats = _pluginLoader.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.LoadOrderPluginCount.Should().Be(0);
        stats.CrashLogPluginCount.Should().Be(2);
        stats.LastOperationDuration.Should().BePositive();
    }
}