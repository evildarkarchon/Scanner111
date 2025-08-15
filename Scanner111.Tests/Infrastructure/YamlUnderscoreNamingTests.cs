using Scanner111.Core.Models.Yaml;
using Scanner111.Tests.TestHelpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Tests for YAML deserialization with underscore naming convention
/// </summary>
[Collection("Parser Tests")]
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
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.ClassicInfo.Should().NotBeNull("because classic_info section should be mapped");
        result.ClassicInfo.Version.Should().Be("CLASSIC v7.35.0", "because version should map correctly");
        result.ClassicInfo.VersionDate.Should()
            .Be("25.06.11", "because version_date should map with underscore convention");
        result.ClassicInfo.IsPrerelease.Should().BeTrue("because is_prerelease should map correctly");
        result.ClassicInfo.DefaultSettings.Should()
            .Be("Test settings content", "because default_settings should map correctly");
        result.ClassicInfo.DefaultLocalYaml.Should()
            .Be("Test local yaml", "because default_local_yaml should map correctly");
        result.ClassicInfo.DefaultIgnorefile.Should()
            .Be("Test ignore file", "because default_ignorefile should map correctly");
    }

    [Fact(Skip = "ClassicFallout4Yaml removed in migration to V2")]
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
        // Commented out - ClassicFallout4Yaml removed in migration to V2
        // var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);

        // All assertions commented out as ClassicFallout4Yaml has been removed
        /*
        var result = new { GameInfo = new GameInfo() }; // Temporary placeholder

        // Assert
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.GameInfo.Should().NotBeNull("because game_info section should be mapped");
        result.GameInfo.MainRootName.Should().Be("Fallout 4", "because main_root_name should map correctly");
        result.GameInfo.MainDocsName.Should().Be("Fallout4", "because main_docs_name should map correctly");
        result.GameInfo.MainSteamId.Should().Be(377160, "because main_steam_id should map correctly");
        result.GameInfo.ExeHashedOld.Should().Be("55f57947db9e05575122fae1088f0b0247442f11e566b56036caa0ac93329c36", "because exe_hashed_old should map correctly");
        result.GameInfo.ExeHashedNew.Should().Be("bcb8f9fe660ef4c33712b873fdc24e5ecbd6a77e629d6419f803c2c09c63eaf2", "because exe_hashed_new should map correctly");
        result.GameInfo.GameVersion.Should().Be("1.10.163", "because game_version should map correctly");
        result.GameInfo.GameVersionNew.Should().Be("1.10.984", "because game_version_new should map correctly");
        result.GameInfo.CrashgenAcronym.Should().Be("BO4", "because crashgen_acronym should map correctly");
        result.GameInfo.CrashgenLogName.Should().Be("Buffout 4", "because crashgen_log_name should map correctly");
        result.GameInfo.CrashgenDllFile.Should().Be("buffout4.dll", "because crashgen_dll_file should map correctly");
        result.GameInfo.CrashgenLatestVer.Should().Be("Buffout 4 v1.28.6", "because crashgen_latest_ver should map correctly");
        result.GameInfo.CrashgenIgnore.Should().Contain("F4EE", "because crashgen_ignore list should include F4EE");
        result.GameInfo.CrashgenIgnore.Should().Contain("WaitForDebugger", "because crashgen_ignore list should include WaitForDebugger");
        result.GameInfo.CrashgenIgnore.Should().Contain("Achievements", "because crashgen_ignore list should include Achievements");
        result.GameInfo.XseAcronym.Should().Be("F4SE", "because xse_acronym should map correctly");
        result.GameInfo.XseFullName.Should().Be("Fallout 4 Script Extender (F4SE)", "because xse_full_name should map correctly");
        result.GameInfo.XseVerLatest.Should().Be("0.6.23", "because xse_ver_latest should map correctly");
        result.GameInfo.XseVerLatestNg.Should().Be("0.7.2", "because xse_ver_latest_ng should map correctly");
        result.GameInfo.XseFileCount.Should().Be(29, "because xse_file_count should map correctly");
        */
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
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.CatchLogErrors.Should().Contain("critical", "because catch_log_errors should include critical");
        result.CatchLogErrors.Should().Contain("error", "because catch_log_errors should include error");
        result.CatchLogErrors.Should().Contain("failed", "because catch_log_errors should include failed");

        result.CatchLogRecords.Should().Contain(".bgsm", "because catch_log_records should include .bgsm");
        result.CatchLogRecords.Should().Contain(".dds", "because catch_log_records should include .dds");
        result.CatchLogRecords.Should().Contain(".dll+", "because catch_log_records should include .dll+");

        result.ExcludeLogRecords.Should().Contain("(Main*)", "because exclude_log_records should include (Main*)");
        result.ExcludeLogRecords.Should().Contain("(size_t)", "because exclude_log_records should include (size_t)");
        result.ExcludeLogRecords.Should().Contain("(void*)", "because exclude_log_records should include (void*)");

        result.ExcludeLogErrors.Should().Contain("failed to get next record",
            "because exclude_log_errors should include this error");
        result.ExcludeLogErrors.Should()
            .Contain("failed to open pdb", "because exclude_log_errors should include this error");

        result.ExcludeLogFiles.Should().Contain("cbpfo4", "because exclude_log_files should include cbpfo4");
        result.ExcludeLogFiles.Should().Contain("crash-", "because exclude_log_files should include crash-");
        result.ExcludeLogFiles.Should().Contain("CreationKit", "because exclude_log_files should include CreationKit");
    }

    [Fact(Skip = "ClassicFallout4Yaml removed in migration to V2")]
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
        // Commented out - ClassicFallout4Yaml removed in migration to V2
        // var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);

        // All assertions commented out as ClassicFallout4Yaml has been removed
        /*
        var result = new { GameInfo = new GameInfo() }; // Temporary placeholder

        // Assert
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.CrashlogRecordsExclude.Should().Contain("\"\"", "because crashlog_records_exclude should include empty quotes");
        result.CrashlogRecordsExclude.Should().Contain("...", "because crashlog_records_exclude should include ellipsis");
        result.CrashlogRecordsExclude.Should().Contain("FE:", "because crashlog_records_exclude should include FE:");

        result.CrashlogPluginsExclude.Should().Contain("Buffout4.dll", "because crashlog_plugins_exclude should include Buffout4.dll");
        result.CrashlogPluginsExclude.Should().Contain("Fallout4.esm", "because crashlog_plugins_exclude should include Fallout4.esm");
        result.CrashlogPluginsExclude.Should().Contain("DLCCoast.esm", "because crashlog_plugins_exclude should include DLCCoast.esm");

        result.CrashlogErrorCheck.Keys.Should().Contain("5 | Stack Overflow Crash", "because crashlog_error_check should have this key");
        result.CrashlogErrorCheck["5 | Stack Overflow Crash"].Should().Be("EXCEPTION_STACK_OVERFLOW", "because stack overflow crash should map to this value");
        result.CrashlogErrorCheck["3 | C++ Redist Crash"].Should().Be("MSVC", "because C++ redist crash should map to MSVC");
        result.CrashlogErrorCheck["4 | Rendering Crash"].Should().Be("d3d11", "because rendering crash should map to d3d11");

        result.CrashlogStackCheck.Keys.Should().Contain("5 | Scaleform Gfx Crash", "because crashlog_stack_check should have this key");
        result.CrashlogStackCheck["5 | Scaleform Gfx Crash"].Should().Contain("ME-OPT|Scaleform::Gfx::Value::ObjectInterface", "because scaleform crash should include this stack entry");
        result.CrashlogStackCheck["5 | Scaleform Gfx Crash"].Should().Contain("InstalledContentPanelBackground_mc", "because scaleform crash should include this stack entry");
        result.CrashlogStackCheck["5 | DLL Crash"].Should().Contain("DLCBannerDLC01.dds", "because DLL crash should include this stack entry");
        */
    }

    [Fact(Skip = "ClassicFallout4Yaml removed in migration to V2")]
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
        // Commented out - ClassicFallout4Yaml removed in migration to V2
        // var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);

        // All assertions commented out as ClassicFallout4Yaml has been removed
        /*
        var result = new { GameInfo = new GameInfo() }; // Temporary placeholder

        // Assert
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.BackupEnb.Should().Contain("enbseries", "because backup_enb should include enbseries");
        result.BackupEnb.Should().Contain("d3d11.dll", "because backup_enb should include d3d11.dll");
        result.BackupEnb.Should().Contain("enblocal.ini", "because backup_enb should include enblocal.ini");

        result.BackupReshade.Should().Contain("ReShade.ini", "because backup_reshade should include ReShade.ini");
        result.BackupReshade.Should().Contain("ReShadePreset.ini", "because backup_reshade should include ReShadePreset.ini");
        result.BackupReshade.Should().Contain("reshade-shaders", "because backup_reshade should include reshade-shaders");

        result.BackupVulkan.Should().Contain("Vulkan.msi", "because backup_vulkan should include Vulkan.msi");
        result.BackupVulkan.Should().Contain("vulkan_d3d11.dll", "because backup_vulkan should include vulkan_d3d11.dll");
        result.BackupVulkan.Should().Contain("dxgi.dll", "because backup_vulkan should include dxgi.dll");

        result.BackupXse.Should().Contain("CustomControlMap.txt", "because backup_xse should include CustomControlMap.txt");
        result.BackupXse.Should().Contain("f4se_loader.exe", "because backup_xse should include f4se_loader.exe");
        result.BackupXse.Should().Contain("f4se_readme.txt", "because backup_xse should include f4se_readme.txt");
        */
    }

    [Fact(Skip = "ClassicFallout4Yaml removed in migration to V2")]
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
        // Commented out - ClassicFallout4Yaml removed in migration to V2
        // var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);

        // All assertions commented out as ClassicFallout4Yaml has been removed
        /*
        var result = new { GameInfo = new GameInfo() }; // Temporary placeholder

        // Assert
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.XseHashedScripts["Actor.pex"].Should().Be("9333aa9b33d6009933afc3a1234a89ca93b5522ea186b44bc6c78846ed5a82c4", "because Actor.pex hash should map correctly");
        result.XseHashedScripts["ActorBase.pex"].Should().Be("cb5d29fead7df77eca8674101abdc57349a8cf345f18c3ddd6ef8d94ad254da7", "because ActorBase.pex hash should map correctly");
        result.XseHashedScripts["Form.pex"].Should().Be("3ac9cd7ecb22d377800ca316413eb1d8f4def3ff3721a14b4c6fa61500f9f568", "because Form.pex hash should map correctly");

        result.XseHashedScriptsNew["Actor.pex"].Should().Be("12175169977977bf382631272ae6dfda03f002c268434144eedf8653000b2b90", "because new Actor.pex hash should map correctly");
        result.XseHashedScriptsNew["ActorBase.pex"].Should().Be("6c7f6b82306ef541673ebb31142c5f69d32f574d81f932d957e3e7f3b649863f", "because new ActorBase.pex hash should map correctly");
        result.XseHashedScriptsNew["Form.pex"].Should().Be("7afbf5bdf3e454dbf968c784807c6bef79fa88893083f1160bc4bb4e980228b3", "because new Form.pex hash should map correctly");
        */
    }

    [Fact(Skip = "ClassicFallout4Yaml removed in migration to V2")]
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
        // Commented out - ClassicFallout4Yaml removed in migration to V2
        // var result = _deserializer.Deserialize<ClassicFallout4Yaml>(yaml);

        // All assertions commented out as ClassicFallout4Yaml has been removed
        /*
        var result = new { GameInfo = new GameInfo() }; // Temporary placeholder

        // Assert
        result.Should().NotBeNull("because YAML deserialization should return a valid object");
        result.ModsCore.Keys.Should().Contain("CanarySaveFileMonitor | Canary Save File Monitor", "because mods_core should have this key");
        result.ModsCore.Keys.Should().Contain("HighFPSPhysicsFix | High FPS Physics Fix", "because mods_core should have this key");
        result.ModsCore["CanarySaveFileMonitor | Canary Save File Monitor"].Should().Contain("This is a highly recommended mod", "because canary mod description should be mapped");
        result.ModsCore["HighFPSPhysicsFix | High FPS Physics Fix"].Should().Contain("This is a mandatory patch", "because physics fix description should be mapped");

        result.ModsFreq.Keys.Should().Contain("DamageThresholdFramework", "because mods_freq should have this key");
        result.ModsFreq.Keys.Should().Contain("Endless Warfare", "because mods_freq should have this key");
        result.ModsFreq["DamageThresholdFramework"].Should().Contain("Damage Threshold Framework", "because damage threshold description should be mapped");
        result.ModsFreq["Endless Warfare"].Should().Contain("Endless Warfare", "because endless warfare description should be mapped");
        */
    }

    [Fact]
    public void TestYamlSettingsProvider_LoadYaml_WithActualFiles()
    {
        // This test verifies that our test provider can load the models correctly
        var yamlProvider = new TestYamlSettingsProvider();

        // Test loading ClassicMainYaml
        var mainYaml = yamlProvider.LoadYaml<ClassicMainYaml>("CLASSIC Main");
        mainYaml.Should().NotBeNull("because test provider should load CLASSIC Main YAML successfully");

        // Test loading ClassicFallout4YamlV2
        var fallout4Yaml = yamlProvider.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        fallout4Yaml.Should().NotBeNull("because test provider should load CLASSIC Fallout4 YAML successfully");
    }
}