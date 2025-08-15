using FluentAssertions;
using Scanner111.Core.Models.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Tests for ClassicFallout4YamlV2 deserialization with underscore naming convention
/// </summary>
public class ClassicFallout4YamlV2DeserializationTests
{
    private readonly IDeserializer _deserializer;

    public ClassicFallout4YamlV2DeserializationTests()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    [Fact]
    public void GameInfoV2_WithVersionsDictionary_DeserializesCorrectly()
    {
        // Arrange
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  crashgen_acronym: BO4
  crashgen_log_name: Buffout 4
  crashgen_dll_file: buffout4.dll
  crashgen_ignore:
    - F4EE
    - WaitForDebugger
    - Achievements
  xse_acronym: F4SE
  xse_full_name: Fallout 4 Script Extender (F4SE)
  xse_file_count: 29
  versions:
    pre_ng:
      name: Pre-Next Gen
      game_version: 1.10.163
      exe_hash: 55f57947db9e05575122fae1088f0b0247442f11e566b56036caa0ac93329c36
      xse_version: 0.6.23
      buffout_latest: v1.26.2
      xse_scripts:
        Actor.pex: 9333aa9b33d6009933afc3a1234a89ca93b5522ea186b44bc6c78846ed5a82c4
        Form.pex: 3ac9cd7ecb22d377800ca316413eb1d8f4def3ff3721a14b4c6fa61500f9f568
    next_gen:
      name: Next Gen Update
      game_version: 1.10.984
      exe_hash: bcb8f9fe660ef4c33712b873fdc24e5ecbd6a77e629d6419f803c2c09c63eaf2
      xse_version: 0.7.2
      buffout_latest: v1.28.6
      crashgen_ignore:
        - F4EE_NG
        - NewFeature
      xse_scripts:
        Actor.pex: 12175169977977bf382631272ae6dfda03f002c268434144eedf8653000b2b90
        Form.pex: 7afbf5bdf3e454dbf968c784807c6bef79fa88893083f1160bc4bb4e980228b3
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull("because YAML deserialization should succeed");
        result.GameInfo.Should().NotBeNull("because game_info section should be mapped");

        // Test main game info properties
        result.GameInfo.MainRootName.Should().Be("Fallout 4");
        result.GameInfo.MainDocsName.Should().Be("Fallout4");
        result.GameInfo.MainSteamId.Should().Be(377160);
        result.GameInfo.CrashgenAcronym.Should().Be("BO4");
        result.GameInfo.CrashgenLogName.Should().Be("Buffout 4");
        result.GameInfo.CrashgenDllFile.Should().Be("buffout4.dll");
        result.GameInfo.CrashgenIgnore.Should().HaveCount(3);
        result.GameInfo.CrashgenIgnore.Should().Contain(new[] { "F4EE", "WaitForDebugger", "Achievements" });
        result.GameInfo.XseAcronym.Should().Be("F4SE");
        result.GameInfo.XseFullName.Should().Be("Fallout 4 Script Extender (F4SE)");
        result.GameInfo.XseFileCount.Should().Be(29);

        // Test versions dictionary
        result.GameInfo.Versions.Should().NotBeNull();
        result.GameInfo.Versions.Should().HaveCount(2);
        result.GameInfo.Versions.Should().ContainKeys("pre_ng", "next_gen");

        // Test Pre-NG version
        var preNg = result.GameInfo.Versions["pre_ng"];
        preNg.Should().NotBeNull();
        preNg.Name.Should().Be("Pre-Next Gen");
        preNg.GameVersion.Should().Be("1.10.163");
        preNg.ExeHash.Should().Be("55f57947db9e05575122fae1088f0b0247442f11e566b56036caa0ac93329c36");
        preNg.XseVersion.Should().Be("0.6.23");
        preNg.BuffoutLatest.Should().Be("v1.26.2");
        preNg.XseScripts.Should().HaveCount(2);
        preNg.XseScripts["Actor.pex"].Should().Be("9333aa9b33d6009933afc3a1234a89ca93b5522ea186b44bc6c78846ed5a82c4");
        preNg.XseScripts["Form.pex"].Should().Be("3ac9cd7ecb22d377800ca316413eb1d8f4def3ff3721a14b4c6fa61500f9f568");
        preNg.CrashgenIgnore.Should().BeNull("because pre_ng doesn't override crashgen_ignore");

        // Test Next Gen version
        var nextGen = result.GameInfo.Versions["next_gen"];
        nextGen.Should().NotBeNull();
        nextGen.Name.Should().Be("Next Gen Update");
        nextGen.GameVersion.Should().Be("1.10.984");
        nextGen.ExeHash.Should().Be("bcb8f9fe660ef4c33712b873fdc24e5ecbd6a77e629d6419f803c2c09c63eaf2");
        nextGen.XseVersion.Should().Be("0.7.2");
        nextGen.BuffoutLatest.Should().Be("v1.28.6");
        nextGen.CrashgenIgnore.Should().NotBeNull("because next_gen overrides crashgen_ignore");
        nextGen.CrashgenIgnore.Should().HaveCount(2);
        nextGen.CrashgenIgnore.Should().Contain(new[] { "F4EE_NG", "NewFeature" });
        nextGen.XseScripts.Should().HaveCount(2);
        nextGen.XseScripts["Actor.pex"].Should().Be("12175169977977bf382631272ae6dfda03f002c268434144eedf8653000b2b90");
        nextGen.XseScripts["Form.pex"].Should().Be("7afbf5bdf3e454dbf968c784807c6bef79fa88893083f1160bc4bb4e980228b3");
    }

    [Fact]
    public void BackupConfigurations_DeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  versions: {}
backup_enb:
  - d3d11.dll
  - d3dcompiler_46e.dll
  - enblocal.ini
backup_reshade:
  - dxgi.dll
  - reshade.ini
  - reshade-shaders/
backup_vulkan:
  - vkd3d.dll
  - vulkan-1.dll
  - nvngx.dll
backup_xse:
  - f4se_loader.exe
  - f4se_1_10_163.dll
  - f4se_steam_loader.dll
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();

        result.BackupEnb.Should().NotBeNull();
        result.BackupEnb.Should().HaveCount(3);
        result.BackupEnb.Should().Contain(new[] { "d3d11.dll", "d3dcompiler_46e.dll", "enblocal.ini" });

        result.BackupReshade.Should().NotBeNull();
        result.BackupReshade.Should().HaveCount(3);
        result.BackupReshade.Should().Contain("reshade-shaders/");

        result.BackupVulkan.Should().NotBeNull();
        result.BackupVulkan.Should().HaveCount(3);
        result.BackupVulkan.Should().Contain("nvngx.dll");

        result.BackupXse.Should().NotBeNull();
        result.BackupXse.Should().HaveCount(3);
        result.BackupXse.Should().Contain("f4se_loader.exe");
    }

    [Fact]
    public void WarningConfigurations_DeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  versions: {}
warnings_crashgen:
  warn_toml_achievements: Achievement enabler detected
  warn_toml_memory: Memory settings warning
  warn_toml_f4ee: F4EE detected
  warn_outdated: Buffout 4 is outdated
  warn_missing: Buffout 4 is missing
  warn_no_plugins: No plugins detected
warnings_xse:
  warn_outdated: F4SE is outdated
  warn_missing: F4SE is missing
  warn_mismatch: F4SE version mismatch
warnings_mods:
  warn_adlib_missing: Address Library is missing
  warn_mod_xse_preloader: XSE Preloader detected
  warn_wrye_missing_html: Wrye Bash HTML file missing
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();

        result.WarningsCrashgen.Should().NotBeNull();
        result.WarningsCrashgen.WarnTomlAchievements.Should().Be("Achievement enabler detected");
        result.WarningsCrashgen.WarnTomlMemory.Should().Be("Memory settings warning");
        result.WarningsCrashgen.WarnTomlF4ee.Should().Be("F4EE detected");
        result.WarningsCrashgen.WarnOutdated.Should().Be("Buffout 4 is outdated");
        result.WarningsCrashgen.WarnMissing.Should().Be("Buffout 4 is missing");
        result.WarningsCrashgen.WarnNoPlugins.Should().Be("No plugins detected");

        result.WarningsXse.Should().NotBeNull();
        result.WarningsXse.WarnOutdated.Should().Be("F4SE is outdated");
        result.WarningsXse.WarnMissing.Should().Be("F4SE is missing");
        result.WarningsXse.WarnMismatch.Should().Be("F4SE version mismatch");

        result.WarningsMods.Should().NotBeNull();
        result.WarningsMods.WarnAdlibMissing.Should().Be("Address Library is missing");
        result.WarningsMods.WarnModXsePreloader.Should().Be("XSE Preloader detected");
        result.WarningsMods.WarnWryeMissingHtml.Should().Be("Wrye Bash HTML file missing");
    }

    [Fact]
    public void CrashlogConfigurations_DeserializeCorrectly()
    {
        // Arrange - Test even simpler structure
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  versions: {}
crashlog_error_check:
  Access Violation: access violation
  Null Pointer: null pointer
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();

        result.CrashlogErrorCheck.Should().NotBeNull();
        result.CrashlogErrorCheck.Should().HaveCount(2);
        result.CrashlogErrorCheck["Access Violation"].Should().Be("access violation");
        result.CrashlogErrorCheck["Null Pointer"].Should().Be("null pointer");
    }

    [Fact]
    public void ModConfigurations_DeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  versions: {}
game_hints:
  - Check your load order
  - Verify game files
  - Update your mods
default_custom_ini: Custom.ini
default_fid_mods: '00000800:Fallout4.esm'
mods_core:
  Unofficial Fallout 4 Patch.esp: Essential bug fix mod
  Address Library.dll: Required for many SKSE plugins
mods_core_follon:
  PANPC.esp: Performance optimization
  BakaScrapHeap.dll: Memory management
mods_freq:
  Sim Settlements 2.esm: Popular settlement mod
  True Storms.esp: Weather overhaul
mods_conf:
  LooksMenu.esp: Can conflict with other character mods
  BodySlide.esp: May cause issues with armor mods
mods_solu:
  CTD_Fix.esp: Fixes common crashes
  Engine Fixes.dll: Addresses engine bugs
mods_opc2:
  Mod1.esp: Optional content 1
  Mod2.esp: Optional content 2
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();

        result.GameHints.Should().NotBeNull();
        result.GameHints.Should().HaveCount(3);
        result.GameHints.Should().Contain("Check your load order");
        result.GameHints.Should().Contain("Update your mods");

        result.DefaultCustomIni.Should().Be("Custom.ini");
        result.DefaultFidMods.Should().Be("00000800:Fallout4.esm");

        result.ModsCore.Should().NotBeNull();
        result.ModsCore.Should().HaveCount(2);
        result.ModsCore["Unofficial Fallout 4 Patch.esp"].Should().Be("Essential bug fix mod");
        result.ModsCore["Address Library.dll"].Should().Be("Required for many SKSE plugins");

        result.ModsCoreFollon.Should().NotBeNull();
        result.ModsCoreFollon.Should().HaveCount(2);
        result.ModsCoreFollon["PANPC.esp"].Should().Be("Performance optimization");

        result.ModsFreq.Should().NotBeNull();
        result.ModsFreq.Should().HaveCount(2);
        result.ModsFreq["Sim Settlements 2.esm"].Should().Be("Popular settlement mod");

        result.ModsConf.Should().NotBeNull();
        result.ModsConf.Should().HaveCount(2);
        result.ModsConf["LooksMenu.esp"].Should().Be("Can conflict with other character mods");

        result.ModsSolu.Should().NotBeNull();
        result.ModsSolu.Should().HaveCount(2);
        result.ModsSolu["CTD_Fix.esp"].Should().Be("Fixes common crashes");

        result.ModsOpc2.Should().NotBeNull();
        result.ModsOpc2.Should().HaveCount(2);
        result.ModsOpc2["Mod1.esp"].Should().Be("Optional content 1");
    }

    [Fact]
    public void EmptyVersionsDictionary_DeserializesWithoutError()
    {
        // Arrange
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_docs_name: Fallout4
  main_steam_id: 377160
  crashgen_acronym: BO4
  xse_acronym: F4SE
  xse_file_count: 29
  versions: {}
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.GameInfo.Should().NotBeNull();
        result.GameInfo.Versions.Should().NotBeNull();
        result.GameInfo.Versions.Should().BeEmpty();
    }

    [Fact]
    public void CrashlogStackCheck_DeserializesCorrectly()
    {
        // Arrange - Test only CrashlogStackCheck with Dictionary<string, List<string>>
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_steam_id: 377160
  versions: {}
crashlog_stack_check:
  'Stack Overflow':
    - stack overflow
    - EXCEPTION_STACK_OVERFLOW
  'Invalid Handle':
    - invalid handle
    - bad handle
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.CrashlogStackCheck.Should().NotBeNull();
        result.CrashlogStackCheck.Should().HaveCount(2);
        result.CrashlogStackCheck["Stack Overflow"].Should().HaveCount(2);
        result.CrashlogStackCheck["Stack Overflow"].Should().Contain("stack overflow");
        result.CrashlogStackCheck["Invalid Handle"].Should().Contain("bad handle");
    }

    [Fact]
    public void PartialData_DeserializesWithDefaults()
    {
        // Arrange - minimal YAML
        var yaml = @"
game_info:
  main_root_name: Fallout 4
  main_steam_id: 377160
";

        // Act
        var result = _deserializer.Deserialize<ClassicFallout4YamlV2>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.GameInfo.Should().NotBeNull();
        result.GameInfo.MainRootName.Should().Be("Fallout 4");
        result.GameInfo.MainSteamId.Should().Be(377160);

        // Check defaults
        result.GameInfo.MainDocsName.Should().BeEmpty();
        result.GameInfo.CrashgenAcronym.Should().BeEmpty();
        result.GameInfo.Versions.Should().NotBeNull();
        result.GameInfo.Versions.Should().BeEmpty();
        result.BackupEnb.Should().NotBeNull();
        result.BackupEnb.Should().BeEmpty();
        result.CrashlogRecordsExclude.Should().NotBeNull();
        result.CrashlogRecordsExclude.Should().BeEmpty();
        result.ModsCore.Should().NotBeNull();
        result.ModsCore.Should().BeEmpty();
    }
}