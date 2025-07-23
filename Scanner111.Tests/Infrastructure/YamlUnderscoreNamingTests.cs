using Scanner111.Core.Models.Yaml;
using Scanner111.Tests.TestHelpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Tests for YAML deserialization with underscore naming convention
/// </summary>
public class YamlUnderscoreNamingTests
{
    private readonly IDeserializer _deserializer;

    public YamlUnderscoreNamingTests()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    [Fact]
    public void ClassicInfo_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
classic_info:
  version: CLASSIC v7.35.0
  version_date: 25.06.11
  is_prerelease: true
  default_settings: 'Test settings content'
  default_local_yaml: 'Test local yaml'
  default_ignorefile: 'Test ignore file'
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicMainYaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ClassicInfo);
        Assert.Equal("CLASSIC v7.35.0", result.ClassicInfo.Version);
        Assert.Equal("25.06.11", result.ClassicInfo.VersionDate);
        Assert.True(result.ClassicInfo.IsPrerelease);
        Assert.Equal("Test settings content", result.ClassicInfo.DefaultSettings);
        Assert.Equal("Test local yaml", result.ClassicInfo.DefaultLocalYaml);
        Assert.Equal("Test ignore file", result.ClassicInfo.DefaultIgnorefile);
    }

    [Fact]
    public void GameInfo_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  exe_hashed_old: 55f57947db9e05575122fae1088f0b0247442f11e566b56036caa0ac93329c36
  exe_hashed_new: bcb8f9fe660ef4c33712b873fdc24e5ecbd6a77e629d6419f803c2c09c63eaf2
  game_version: 1.10.163
  game_version_new: 1.10.984
  crashgen_acronym: BO4
  crashgen_log_name: Buffout 4
  crashgen_dll_file: buffout4.dll
  crashgen_latest_ver: Buffout 4 v1.28.6
  crashgen_ignore:
    - F4EE
    - WaitForDebugger
    - Achievements
  xse_acronym: F4SE
  xse_full_name: Fallout 4 Script Extender (F4SE)
  xse_ver_latest: 0.6.23
  xse_ver_latest_ng: 0.7.2
  xse_file_count: 29
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.GameInfo);
        Assert.Equal("Fallout 4", result.GameInfo.MainRootName);
        Assert.Equal("Fallout4", result.GameInfo.MainDocsName);
        Assert.Equal(377160, result.GameInfo.MainSteamId);
        Assert.Equal("55f57947db9e05575122fae1088f0b0247442f11e566b56036caa0ac93329c36", result.GameInfo.ExeHashedOld);
        Assert.Equal("bcb8f9fe660ef4c33712b873fdc24e5ecbd6a77e629d6419f803c2c09c63eaf2", result.GameInfo.ExeHashedNew);
        Assert.Equal("1.10.163", result.GameInfo.GameVersion);
        Assert.Equal("1.10.984", result.GameInfo.GameVersionNew);
        Assert.Equal("BO4", result.GameInfo.CrashgenAcronym);
        Assert.Equal("Buffout 4", result.GameInfo.CrashgenLogName);
        Assert.Equal("buffout4.dll", result.GameInfo.CrashgenDllFile);
        Assert.Equal("Buffout 4 v1.28.6", result.GameInfo.CrashgenLatestVer);
        Assert.Contains("F4EE", result.GameInfo.CrashgenIgnore);
        Assert.Contains("WaitForDebugger", result.GameInfo.CrashgenIgnore);
        Assert.Contains("Achievements", result.GameInfo.CrashgenIgnore);
        Assert.Equal("F4SE", result.GameInfo.XseAcronym);
        Assert.Equal("Fallout 4 Script Extender (F4SE)", result.GameInfo.XseFullName);
        Assert.Equal("0.6.23", result.GameInfo.XseVerLatest);
        Assert.Equal("0.7.2", result.GameInfo.XseVerLatestNg);
        Assert.Equal(29, result.GameInfo.XseFileCount);
    }

    [Fact]
    public void MainYamlCollections_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
catch_log_errors:
  - critical
  - error
  - failed

catch_log_records:
  - .bgsm
  - .dds
  - .dll+

exclude_log_records:
  - (Main*)
  - (size_t)
  - (void*)

exclude_log_errors:
  - failed to get next record
  - failed to open pdb

exclude_log_files:
  - cbpfo4
  - crash-
  - CreationKit
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicMainYaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("critical", result.CatchLogErrors);
        Assert.Contains("error", result.CatchLogErrors);
        Assert.Contains("failed", result.CatchLogErrors);
        
        Assert.Contains(".bgsm", result.CatchLogRecords);
        Assert.Contains(".dds", result.CatchLogRecords);
        Assert.Contains(".dll+", result.CatchLogRecords);
        
        Assert.Contains("(Main*)", result.ExcludeLogRecords);
        Assert.Contains("(size_t)", result.ExcludeLogRecords);
        Assert.Contains("(void*)", result.ExcludeLogRecords);
        
        Assert.Contains("failed to get next record", result.ExcludeLogErrors);
        Assert.Contains("failed to open pdb", result.ExcludeLogErrors);
        
        Assert.Contains("cbpfo4", result.ExcludeLogFiles);
        Assert.Contains("crash-", result.ExcludeLogFiles);
        Assert.Contains("CreationKit", result.ExcludeLogFiles);
    }

    [Fact]
    public void Fallout4YamlCollections_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
crashlog_records_exclude:
  - '""""'
  - '...'
  - 'FE:'

crashlog_plugins_exclude:
  - Buffout4.dll
  - Fallout4.esm
  - DLCCoast.esm

crashlog_error_check:
  '5 | Stack Overflow Crash': 'EXCEPTION_STACK_OVERFLOW'
  '3 | C++ Redist Crash': 'MSVC'
  '4 | Rendering Crash': 'd3d11'

crashlog_stack_check:
  '5 | Scaleform Gfx Crash':
    - 'ME-OPT|Scaleform::Gfx::Value::ObjectInterface'
    - 'InstalledContentPanelBackground_mc'
  '5 | DLL Crash':
    - 'DLCBannerDLC01.dds'
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"\"", result.CrashlogRecordsExclude);
        Assert.Contains("...", result.CrashlogRecordsExclude);
        Assert.Contains("FE:", result.CrashlogRecordsExclude);
        
        Assert.Contains("Buffout4.dll", result.CrashlogPluginsExclude);
        Assert.Contains("Fallout4.esm", result.CrashlogPluginsExclude);
        Assert.Contains("DLCCoast.esm", result.CrashlogPluginsExclude);
        
        Assert.Contains("5 | Stack Overflow Crash", result.CrashlogErrorCheck.Keys);
        Assert.Equal("EXCEPTION_STACK_OVERFLOW", result.CrashlogErrorCheck["5 | Stack Overflow Crash"]);
        Assert.Equal("MSVC", result.CrashlogErrorCheck["3 | C++ Redist Crash"]);
        Assert.Equal("d3d11", result.CrashlogErrorCheck["4 | Rendering Crash"]);
        
        Assert.Contains("5 | Scaleform Gfx Crash", result.CrashlogStackCheck.Keys);
        Assert.Contains("ME-OPT|Scaleform::Gfx::Value::ObjectInterface", result.CrashlogStackCheck["5 | Scaleform Gfx Crash"]);
        Assert.Contains("InstalledContentPanelBackground_mc", result.CrashlogStackCheck["5 | Scaleform Gfx Crash"]);
        Assert.Contains("DLCBannerDLC01.dds", result.CrashlogStackCheck["5 | DLL Crash"]);
    }

    [Fact]
    public void BackupCollections_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
backup_enb:
  - enbseries
  - d3d11.dll
  - enblocal.ini

backup_reshade:
  - ReShade.ini
  - ReShadePreset.ini
  - reshade-shaders

backup_vulkan:
  - Vulkan.msi
  - vulkan_d3d11.dll
  - dxgi.dll

backup_xse:
  - CustomControlMap.txt
  - f4se_loader.exe
  - f4se_readme.txt
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("enbseries", result.BackupEnb);
        Assert.Contains("d3d11.dll", result.BackupEnb);
        Assert.Contains("enblocal.ini", result.BackupEnb);
        
        Assert.Contains("ReShade.ini", result.BackupReshade);
        Assert.Contains("ReShadePreset.ini", result.BackupReshade);
        Assert.Contains("reshade-shaders", result.BackupReshade);
        
        Assert.Contains("Vulkan.msi", result.BackupVulkan);
        Assert.Contains("vulkan_d3d11.dll", result.BackupVulkan);
        Assert.Contains("dxgi.dll", result.BackupVulkan);
        
        Assert.Contains("CustomControlMap.txt", result.BackupXse);
        Assert.Contains("f4se_loader.exe", result.BackupXse);
        Assert.Contains("f4se_readme.txt", result.BackupXse);
    }

    [Fact]
    public void HashMaps_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
xse_hashed_scripts:
  Actor.pex: 9333aa9b33d6009933afc3a1234a89ca93b5522ea186b44bc6c78846ed5a82c4
  ActorBase.pex: cb5d29fead7df77eca8674101abdc57349a8cf345f18c3ddd6ef8d94ad254da7
  Form.pex: 3ac9cd7ecb22d377800ca316413eb1d8f4def3ff3721a14b4c6fa61500f9f568

xse_hashed_scripts_new:
  Actor.pex: 12175169977977bf382631272ae6dfda03f002c268434144eedf8653000b2b90
  ActorBase.pex: 6c7f6b82306ef541673ebb31142c5f69d32f574d81f932d957e3e7f3b649863f
  Form.pex: 7afbf5bdf3e454dbf968c784807c6bef79fa88893083f1160bc4bb4e980228b3
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("9333aa9b33d6009933afc3a1234a89ca93b5522ea186b44bc6c78846ed5a82c4", result.XseHashedScripts["Actor.pex"]);
        Assert.Equal("cb5d29fead7df77eca8674101abdc57349a8cf345f18c3ddd6ef8d94ad254da7", result.XseHashedScripts["ActorBase.pex"]);
        Assert.Equal("3ac9cd7ecb22d377800ca316413eb1d8f4def3ff3721a14b4c6fa61500f9f568", result.XseHashedScripts["Form.pex"]);
        
        Assert.Equal("12175169977977bf382631272ae6dfda03f002c268434144eedf8653000b2b90", result.XseHashedScriptsNew["Actor.pex"]);
        Assert.Equal("6c7f6b82306ef541673ebb31142c5f69d32f574d81f932d957e3e7f3b649863f", result.XseHashedScriptsNew["ActorBase.pex"]);
        Assert.Equal("7afbf5bdf3e454dbf968c784807c6bef79fa88893083f1160bc4bb4e980228b3", result.XseHashedScriptsNew["Form.pex"]);
    }

    [Fact]
    public void ModsCollections_WithUnderscoreConvention_MapsCorrectly()
    {
        // Arrange
        var yaml = @"
mods_core:
  'CanarySaveFileMonitor | Canary Save File Monitor': |
    This is a highly recommended mod that can detect save file corruption.
    Link: https://www.nexusmods.com/fallout4/mods/44949?tab=files
  'HighFPSPhysicsFix | High FPS Physics Fix': |
    This is a mandatory patch / fix that prevents game engine problems.
    Link: https://www.nexusmods.com/fallout4/mods/44798?tab=files

mods_freq:
  'DamageThresholdFramework': |
    Damage Threshold Framework
        - Can cause crashes in combat on some occasions due to how damage calculations are done.
        -----
  'Endless Warfare': |
    Endless Warfare
        - Some enemy spawn points could be bugged or crash the game due to scripts or pathfinding.
        -----
";
        
        // Act
        var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("CanarySaveFileMonitor | Canary Save File Monitor", result.ModsCore.Keys);
        Assert.Contains("HighFPSPhysicsFix | High FPS Physics Fix", result.ModsCore.Keys);
        Assert.Contains("This is a highly recommended mod", result.ModsCore["CanarySaveFileMonitor | Canary Save File Monitor"]);
        Assert.Contains("This is a mandatory patch", result.ModsCore["HighFPSPhysicsFix | High FPS Physics Fix"]);
        
        Assert.Contains("DamageThresholdFramework", result.ModsFreq.Keys);
        Assert.Contains("Endless Warfare", result.ModsFreq.Keys);
        Assert.Contains("Damage Threshold Framework", result.ModsFreq["DamageThresholdFramework"]);
        Assert.Contains("Endless Warfare", result.ModsFreq["Endless Warfare"]);
    }

    [Fact]
    public void TestYamlSettingsProvider_LoadYaml_WithActualFiles()
    {
        // This test verifies that our test provider can load the models correctly
        var yamlProvider = new TestYamlSettingsProvider();
        
        // Test loading ClassicMainYaml
        var mainYaml = yamlProvider.LoadYaml<ClassicMainYaml>("CLASSIC Main");
        Assert.NotNull(mainYaml);
        
        // Test loading ClassicFallout4Yaml  
        var fallout4Yaml = yamlProvider.LoadYaml<ClassicFallout4Yaml>("CLASSIC Fallout4");
        Assert.NotNull(fallout4Yaml);
    }
}