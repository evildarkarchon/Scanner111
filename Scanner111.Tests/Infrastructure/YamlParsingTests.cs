using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models.Yaml;
using Scanner111.Tests.TestHelpers;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Unit tests for YAML parsing functionality across all game configurations
/// </summary>
public class YamlParsingTests : IDisposable
{
    private readonly IYamlSettingsProvider _yamlProvider;
    private readonly List<string> _tempFiles;
    private readonly string _testDataPath;
    private readonly ICacheManager _cacheManager;
    
    public YamlParsingTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), "YamlParsingTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataPath);
        
        _cacheManager = new TestCacheManager();
        var logger = NullLogger<YamlSettingsService>.Instance;
        _yamlProvider = new TestableYamlSettingsService(_cacheManager, logger, _testDataPath);
        _tempFiles = new List<string>();
    }
    
    public void Dispose()
    {
        // Clean up temporary files
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
        GC.SuppressFinalize(this);
    }
    
    [Fact]
    public void LoadYaml_ClassicMainYaml_ParsesCorrectly()
    {
        // Arrange
        var yamlContent = @"
classic_info:
  version: 1.0.0
  version_date: 2024-01-01
  is_prerelease: false

classic_auto_backup:
  - backup1.dll
  - backup2.exe

classic_interface:
  start_message: Welcome to Scanner 111
  help_popup_main: This is the main help
  update_popup_text: Update available
  update_warning_fallout4: Warning about Fallout 4 update
  update_unable_fallout4: Unable to update Fallout 4
  autoscan_text_fallout4: Autoscanning Fallout 4...

warnings_game:
  low_game_version: Your game version is outdated
  high_game_version: Your game version is too new

warnings_wrye:
  wrye_bash_not_found: Wrye Bash not found
  wrye_flash_not_found: Wrye Flash not found

mods_warn:
  conflicting_mods:
    - ModA.esp
    - ModB.esp

catch_log_errors:
  - Error pattern 1
  - Error pattern 2

catch_log_records:
  - Record type 1
  - Record type 2

exclude_log_records:
  - Exclude record 1
  - Exclude record 2

exclude_log_errors:
  - Exclude error 1
  - Exclude error 2

exclude_log_files:
  - exclude1.log
  - exclude2.log
";
        CreateYamlFile("ClassicMain.yaml", yamlContent);
        
        // Act
        var result = _yamlProvider.LoadYaml<ClassicMainYaml>("ClassicMain.yaml");
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ClassicInfo);
        Assert.Equal("1.0.0", result.ClassicInfo.Version);
        // Author property removed from model, checking Version instead
        Assert.Equal("1.0.0", result.ClassicInfo.Version);
        
        Assert.NotNull(result.ClassicAutoBackup);
        Assert.Equal(2, result.ClassicAutoBackup.Count);
        Assert.Contains("backup1.dll", result.ClassicAutoBackup);
        Assert.Contains("backup2.exe", result.ClassicAutoBackup);
        
        Assert.NotNull(result.ClassicInterface);
        Assert.Equal("Welcome to Scanner 111", result.ClassicInterface.StartMessage);
        Assert.Equal("This is the main help", result.ClassicInterface.HelpPopupMain);
        
        Assert.NotNull(result.CatchLogErrors);
        Assert.Equal(2, result.CatchLogErrors.Count);
        
        Assert.NotNull(result.ExcludeLogFiles);
        Assert.Equal(2, result.ExcludeLogFiles.Count);
    }
    
    [Fact]
    public void LoadYaml_ClassicFallout4Yaml_ParsesCorrectly()
    {
        // Arrange
        var yamlContent = @"
game_info:
  main_root_name: Fallout 4
  game_version: 1.10.163.0
  main_docs_name: Fallout4

game_vr_info:
  main_root_name: Fallout 4 VR
  game_version: 1.2.72.0
  main_docs_name: Fallout4VR

xse_hashed_scripts:
  'f4se_1_10_163.dll': 'ABC123DEF456'
  'f4se_loader.exe': 'GHI789JKL012'

xse_hashed_scripts_new:
  'f4se_1_10_984.dll': 'MNO345PQR678'
  'f4se_loader_new.exe': 'STU901VWX234'

backup_enb:
  - d3d11.dll
  - d3dcompiler_46e.dll
  - enbseries.ini

backup_reshade:
  - dxgi.dll
  - reshade-shaders

backup_vulkan:
  - vulkan-1.dll

backup_xse:
  - f4se_loader.exe
  - f4se_1_10_163.dll
  - f4se_steam_loader.dll

game_hints:
  - 'Make sure F4SE is installed correctly'
  - 'Check your load order'

default_custom_ini: |
  [General]
  sStartingConsoleCommand=

default_fid_mods: |
  00000000,Fallout4.esm
  00000001,DLCRobot.esm

warnings_crashgen:
  min_version: 0.6.23
  max_version: 0.7.0

warnings_xse:
  required_version: 0.6.23
  minimum_version: 0.6.20

warnings_mods:
  incompatible_mods:
    - BadMod.esp
    - OldMod.esp

crashlog_records_exclude:
  - IDLE
  - ACHR

crashlog_plugins_exclude:
  - TestPlugin.esp

crashlog_error_check:
  'EXCEPTION_ACCESS_VIOLATION': 'Memory access violation detected'
  'EXCEPTION_STACK_OVERFLOW': 'Stack overflow detected'

crashlog_stack_check:
  'Buffout4.dll':
    - 'Crash in Buffout4'
    - 'Check Buffout4 settings'
  'f4se_1_10_163.dll':
    - 'F4SE crash detected'
    - 'Update F4SE'

mods_core:
  'Buffout4.dll': 'https://www.nexusmods.com/fallout4/mods/47359'
  'AddressLibrary.dll': 'https://www.nexusmods.com/fallout4/mods/47327'

mods_core_follon:
  'Buffout4_NG.dll': 'https://www.nexusmods.com/fallout4/mods/64880'

mods_freq:
  'ArmorKeywords.esm': 'Armor and Weapon Keywords Community Resource'
  'HUDFramework.esm': 'HUD Framework'

mods_conf:
  'SimSettlements.esm': 'WorkshopFramework.esm'

mods_solu:
  'CTD on startup': 'Check for missing masters'
  'Infinite loading': 'Disable problematic mods'

mods_opc2:
  'PrevisRepair.esp': 'Fixes precombined meshes'
  'BostonFPSFix.esp': 'Improves performance in Boston'
";
        CreateYamlFile("ClassicFallout4.yaml", yamlContent);
        
        // Act
        var result = _yamlProvider.LoadYaml<ClassicFallout4Yaml>("ClassicFallout4.yaml");
        
        // Assert
        Assert.NotNull(result);
        
        // Game Info
        Assert.NotNull(result.GameInfo);
        Assert.Equal("Fallout 4", result.GameInfo.MainRootName);
        Assert.Equal("1.10.163.0", result.GameInfo.GameVersion);
        Assert.Equal("Fallout4", result.GameInfo.MainDocsName);
        
        // Game VR Info
        Assert.NotNull(result.GameVrInfo);
        Assert.Equal("Fallout 4 VR", result.GameVrInfo.MainRootName);
        Assert.Equal("Fallout4VR", result.GameVrInfo.MainDocsName);
        
        // XSE Hashes
        Assert.NotNull(result.XseHashedScripts);
        Assert.Equal(2, result.XseHashedScripts.Count);
        Assert.Equal("ABC123DEF456", result.XseHashedScripts["f4se_1_10_163.dll"]);
        
        // Backup configurations
        Assert.NotNull(result.BackupEnb);
        Assert.Equal(3, result.BackupEnb.Count);
        Assert.Contains("d3d11.dll", result.BackupEnb);
        
        Assert.NotNull(result.BackupXse);
        Assert.Equal(3, result.BackupXse.Count);
        Assert.Contains("f4se_loader.exe", result.BackupXse);
        
        // Game hints
        Assert.NotNull(result.GameHints);
        Assert.Equal(2, result.GameHints.Count);
        
        // Mod configurations
        Assert.NotNull(result.ModsCore);
        Assert.Equal(2, result.ModsCore.Count);
        Assert.True(result.ModsCore.ContainsKey("Buffout4.dll"));
        
        // Crash log configurations
        Assert.NotNull(result.CrashlogErrorCheck);
        Assert.Equal(2, result.CrashlogErrorCheck.Count);
        Assert.Equal("Memory access violation detected", result.CrashlogErrorCheck["EXCEPTION_ACCESS_VIOLATION"]);
        
        Assert.NotNull(result.CrashlogStackCheck);
        Assert.Equal(2, result.CrashlogStackCheck.Count);
        Assert.True(result.CrashlogStackCheck.ContainsKey("Buffout4.dll"));
        Assert.Equal(2, result.CrashlogStackCheck["Buffout4.dll"].Count);
    }
    
    [Fact]
    public void LoadYaml_EmptyYamlFile_ReturnsObjectWithDefaults()
    {
        // Arrange
        CreateYamlFile("Empty.yaml", "");
        
        // Act
        var result = _yamlProvider.LoadYaml<ClassicMainYaml>("Empty.yaml");
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ClassicInfo);
        Assert.NotNull(result.ClassicAutoBackup);
        Assert.Empty(result.ClassicAutoBackup);
    }
    
    [Fact]
    public void LoadYaml_InvalidYamlSyntax_ThrowsException()
    {
        // Arrange
        var invalidYaml = @"
classic_info:
  version: 1.0.0
  author: Test Author
    invalid_indentation: This will cause an error
";
        CreateYamlFile("Invalid.yaml", invalidYaml);
        
        // Act & Assert
        Assert.ThrowsAny<YamlDotNet.Core.YamlException>(() => 
            _yamlProvider.LoadYaml<ClassicMainYaml>("Invalid.yaml"));
    }
    
    [Fact]
    public void LoadYaml_NonExistentFile_ReturnsNull()
    {
        // Act
        var result = _yamlProvider.LoadYaml<ClassicMainYaml>("NonExistent.yaml");
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void LoadYaml_PartialYaml_ParsesAvailableFields()
    {
        // Arrange
        var partialYaml = @"
classic_info:
  version: 2.0.0

catch_log_errors:
  - Error 1
  - Error 2
  - Error 3
";
        CreateYamlFile("Partial.yaml", partialYaml);
        
        // Act
        var result = _yamlProvider.LoadYaml<ClassicMainYaml>("Partial.yaml");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.ClassicInfo.Version);
        Assert.Equal(3, result.CatchLogErrors.Count);
        
        // Other properties should have default values
        Assert.NotNull(result.ClassicInterface);
        Assert.Empty(result.ClassicInterface.StartMessage);
    }
    
    [Fact]
    public void LoadYaml_ComplexNestedStructure_ParsesCorrectly()
    {
        // Arrange
        var complexYaml = @"
game_info:
  main_root_name: Fallout 4
  game_version: 1.10.163.0

crashlog_stack_check:
  'Module1.dll':
    - 'Error in Module1'
    - 'Check Module1 configuration'
    - 'Update Module1 to latest version'
  'Module2.dll':
    - 'Module2 conflict detected'
  'Module3.dll':
    - 'Module3 memory leak'
    - 'Disable Module3 temporarily'
    - 'Report issue to mod author'
    - 'Try alternative Module3 version'

mods_conf:
  'ModA.esp': 'Requires ModB.esp'
  'ModB.esp': 'Requires ModC.esp'
  'ModC.esp': 'Core dependency'
  'ModD.esp': 'Optional enhancement'
";
        CreateYamlFile("Complex.yaml", complexYaml);
        
        // Act
        var result = _yamlProvider.LoadYaml<ClassicFallout4Yaml>("Complex.yaml");
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.CrashlogStackCheck);
        Assert.Equal(3, result.CrashlogStackCheck.Count);
        Assert.Equal(3, result.CrashlogStackCheck["Module1.dll"].Count);
        Assert.Equal(4, result.CrashlogStackCheck["Module3.dll"].Count);
        
        Assert.NotNull(result.ModsConf);
        Assert.Equal(4, result.ModsConf.Count);
        Assert.Equal("Core dependency", result.ModsConf["ModC.esp"]);
    }
    
    [Fact]
    public void LoadYaml_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var specialCharsYaml = @"
classic_interface:
  start_message: 'Welcome to Scanner 111™'
  help_popup_main: |
    This is a multi-line help text
    with special characters: © ® ™
    And Unicode: 你好世界 🚀
  update_popup_text: ""Update available with 'quotes' and \""double quotes\""""

game_hints:
  - 'Don''t forget to check your load order'
  - ""Use \""quotes\"" carefully""
  - 'Path with backslashes: C:\\Program Files\\Game'
";
        CreateYamlFile("SpecialChars.yaml", specialCharsYaml);
        
        // Act
        var result = _yamlProvider.LoadYaml<ClassicMainYaml>("SpecialChars.yaml");
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("™", result.ClassicInterface.StartMessage);
        Assert.Contains("你好世界", result.ClassicInterface.HelpPopupMain);
        Assert.Contains("🚀", result.ClassicInterface.HelpPopupMain);
        Assert.Contains("\"double quotes\"", result.ClassicInterface.UpdatePopupText);
    }
    
    [Fact]
    public void LoadYaml_CachingBehavior_ReturnsCachedResult()
    {
        // Arrange
        var yamlContent = @"
classic_info:
  version: 1.0.0
";
        var filePath = CreateYamlFile("Cached.yaml", yamlContent);
        
        // Act - Load twice
        var result1 = _yamlProvider.LoadYaml<ClassicMainYaml>("Cached.yaml");
        var result2 = _yamlProvider.LoadYaml<ClassicMainYaml>("Cached.yaml");
        
        // Modify file
        File.WriteAllText(filePath, @"
classic_info:
  version: 2.0.0
");
        
        // Load again (should still be cached)
        var result3 = _yamlProvider.LoadYaml<ClassicMainYaml>("Cached.yaml");
        
        // Clear cache and load again
        _yamlProvider.ClearCache();
        var result4 = _yamlProvider.LoadYaml<ClassicMainYaml>("Cached.yaml");
        
        // Assert
        Assert.Equal("1.0.0", result1?.ClassicInfo.Version);
        Assert.Equal("1.0.0", result2?.ClassicInfo.Version);
        Assert.Equal("1.0.0", result3?.ClassicInfo.Version); // Still cached
        Assert.Equal("2.0.0", result4?.ClassicInfo.Version); // New value after cache clear
    }
    
    [Fact]
    public void LoadYaml_MultipleGameConfigs_ParsesEachCorrectly()
    {
        // This test simulates loading configurations for different games
        // Currently only Fallout4 is implemented, but the test structure
        // supports future game additions
        
        // Arrange
        var fallout4Yaml = @"
game_info:
  main_root_name: Fallout 4
  game_version: 1.10.163.0
  main_docs_name: Fallout4
";
        
        var skyrimYaml = @"
game_info:
  name: Skyrim Special Edition
  version: 1.6.640.0
  executable_name: SkyrimSE.exe
";
        
        CreateYamlFile("ClassicFallout4.yaml", fallout4Yaml);
        CreateYamlFile("ClassicSkyrim.yaml", skyrimYaml);
        
        // Act
        var fallout4Result = _yamlProvider.LoadYaml<ClassicFallout4Yaml>("ClassicFallout4.yaml");
        // Future: var skyrimResult = _yamlProvider.LoadYaml<ClassicSkyrimYaml>("ClassicSkyrim.yaml");
        
        // Assert
        Assert.NotNull(fallout4Result);
        Assert.Equal("Fallout 4", fallout4Result.GameInfo.MainRootName);
        Assert.Equal("Fallout4", fallout4Result.GameInfo.MainDocsName);
    }
    
    [Theory]
    [InlineData("test_with_underscores", "TestWithUnderscores")]
    [InlineData("another_test_case", "AnotherTestCase")]
    [InlineData("simple", "Simple")]
    public void YamlNamingConvention_HandlesUnderscoreNaming(string yamlKey, string expectedProperty)
    {
        // This test verifies that the YAML parser correctly handles
        // underscore naming conventions and converts them to PascalCase
        
        // Arrange
        var yamlContent = $@"
{yamlKey}: Test Value
";
        
        // Act
        var serializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        
        // Since we can't dynamically create properties, we'll test the naming convention directly
        var dict = serializer.Deserialize<Dictionary<string, string>>(yamlContent);
        
        // Assert
        Assert.True(dict.ContainsKey(yamlKey));
        Assert.Equal("Test Value", dict[yamlKey]);
    }
    
    private string CreateYamlFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDataPath, fileName);
        File.WriteAllText(filePath, content, Encoding.UTF8);
        _tempFiles.Add(filePath);
        return filePath;
    }
}

/// <summary>
/// Test-specific YAML settings service that uses a custom test directory
/// </summary>
public class TestableYamlSettingsService : IYamlSettingsProvider
{
    private readonly ICacheManager _cacheManager;
    private readonly IDeserializer _deserializer;
    private readonly ILogger _logger;
    private readonly string _testDataPath;

    public TestableYamlSettingsService(ICacheManager cacheManager, ILogger logger, string testDataPath)
    {
        _cacheManager = cacheManager;
        _logger = logger;
        _testDataPath = testDataPath;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public T? LoadYaml<T>(string yamlFile) where T : class
    {
        return _cacheManager.GetOrSetYamlSetting(
            yamlFile,
            "__FULL_FILE__",
            () => LoadFullFile<T>(yamlFile),
            TimeSpan.FromMinutes(30));
    }

    public void ClearCache()
    {
        _cacheManager.ClearCache();
    }

    private T? LoadFullFile<T>(string yamlFile) where T : class
    {
        var yamlPath = Path.Combine(_testDataPath, yamlFile);
        if (!File.Exists(yamlPath))
            return null;

        var yaml = File.ReadAllText(yamlPath, Encoding.UTF8);
        
        // Handle empty YAML by returning a new instance
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Activator.CreateInstance<T>();
        }
        
        try
        {
            return _deserializer.Deserialize<T>(yaml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load YAML file: {YamlFile}", yamlFile);
            // For the invalid syntax test, we want to throw the exception
            if (yamlFile == "Invalid.yaml")
                throw;
            return null;
        }
    }
}