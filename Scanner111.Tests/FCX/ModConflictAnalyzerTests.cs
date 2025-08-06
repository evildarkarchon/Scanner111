using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.FCX;
using Scanner111.Core.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Tests.TestHelpers;
using Xunit;
using FluentAssertions;

namespace Scanner111.Tests.FCX;

/// <summary>
/// Unit tests for the ModConflictAnalyzer class
/// </summary>
public class ModConflictAnalyzerTests
{
    private readonly ModConflictAnalyzer _analyzer;
    private readonly TestModScanner _modScanner;
    private readonly TestApplicationSettingsService _appSettings;

    public ModConflictAnalyzerTests()
    {
        _modScanner = new TestModScanner();
        _appSettings = new TestApplicationSettingsService();
        _analyzer = new ModConflictAnalyzer(
            NullLogger<ModConflictAnalyzer>.Instance,
            _modScanner,
            _appSettings);
    }

    [Fact]
    public async Task AnalyzeAsync_WithTextureDimensionIssues_ReportsCorrectly()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = true, ModsFolder = "C:\\TestMods" });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.TextureDimensionsInvalid,
            FilePath = "Data\\Textures\\test.dds",
            Description = "Invalid texture dimensions",
            AdditionalInfo = "1023x1023"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("the analysis should complete successfully");
        genericResult.HasFindings.Should().BeTrue("texture dimension issues should be detected");
        genericResult.ReportText.Should().Contain("⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️", "invalid texture dimensions should be reported");
        genericResult.ReportText.Should().Contain("Data\\Textures\\test.dds (1023x1023)", "the specific file and dimensions should be included");
        genericResult.ReportText.Should().Contain("Texture (DDS) Crash", "texture crash warning should be present");
    }

    [Fact]
    public async Task AnalyzeAsync_WithIncorrectTextureFormat_ReportsIssues()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = true, ModsFolder = "C:\\TestMods" });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.TextureFormatIncorrect,
            FilePath = "Data\\Textures\\badformat.png",
            Description = "Incorrect texture format"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("incorrect texture format should be detected");
        genericResult.ReportText.Should().Contain("❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓", "format warning should be displayed");
        genericResult.ReportText.Should().Contain("Data\\Textures\\badformat.png", "the problematic file should be listed");
    }

    [Fact]
    public async Task AnalyzeAsync_WithXseScriptConflicts_ReportsWarnings()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings 
        { 
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\Fallout4",
            ModsFolder = "C:\\TestMods"
        });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.XseScriptFile,
            FilePath = "Data\\F4SE\\Plugins\\test.dll",
            Description = "XSE script file conflict"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("XSE script conflicts should be detected");
        genericResult.ReportText.Should().Contain("⚠️ MODS CONTAIN COPIES OF *F4SE* SCRIPT FILES ⚠️", "F4SE script warning should be displayed");
        genericResult.ReportText.Should().Contain("Data\\F4SE\\Plugins\\test.dll", "the conflicting file should be listed");
    }

    [Fact]
    public async Task AnalyzeAsync_WithPrevisFiles_ReportsRecommendations()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = true, ModsFolder = "C:\\TestMods" });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.PrevisFile,
            FilePath = "Data\\Meshes\\Precombined\\test.nif",
            Description = "Previs file"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("previs files should be detected");
        genericResult.ReportText.Should().Contain("🔧 PREVIS FILES DETECTED 🔧", "previs detection header should be displayed");
        genericResult.ReportText.Should().Contain("should load after the PRP.esp plugin", "load order recommendation should be included");
        genericResult.ReportText.Should().Contain("Data\\Meshes\\Precombined\\test.nif", "the previs file should be listed");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultipleIssueTypes_CategorizesProperly()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = true, ModsFolder = "C:\\TestMods" });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.TextureDimensionsInvalid,
            FilePath = "texture1.dds",
            AdditionalInfo = "511x511"
        });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.SoundFormatIncorrect,
            FilePath = "sound.mp3"
        });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.AnimationData,
            FilePath = "anim.hkx"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("multiple issue types should be detected");
        
        // Each issue type should have its own section
        genericResult.ReportText.Should().Contain("⚠️ DDS DIMENSIONS", "texture dimension issues should be reported");
        genericResult.ReportText.Should().Contain("❓ SOUND FILES HAVE INCORRECT FORMAT", "sound format issues should be reported");
        genericResult.ReportText.Should().Contain("💀 BROKEN ANIMATION DATA FILES 💀", "animation data issues should be reported");
    }

    [Fact]
    public async Task AnalyzeAsync_NotInFcxMode_ReturnsNoFindings()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = false });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.TextureDimensionsInvalid,
            FilePath = "texture.dds"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete even when FCX mode is disabled");
        genericResult.HasFindings.Should().BeFalse("no findings should be reported when FCX mode is disabled");
        genericResult.ReportLines.Should().BeEmpty("report should be empty when FCX mode is disabled");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoIssues_ReportsNoFindings()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = true, ModsFolder = "C:\\TestMods" });
        
        // ModScanner has no issues
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.Success.Should().BeTrue("analysis should complete successfully");
        genericResult.HasFindings.Should().BeFalse("no findings should be reported when there are no issues");
    }

    [Fact]
    public async Task AnalyzeAsync_WithSkyrimGame_UsesCorrectAcronym()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings 
        { 
            FcxMode = true,
            DefaultGamePath = "C:\\Games\\SkyrimSE",
            ModsFolder = "C:\\TestMods"
        });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.XseScriptFile,
            FilePath = "Data\\SKSE\\Plugins\\test.dll"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.ReportText.Should().Contain("⚠️ MODS CONTAIN COPIES OF *SKSE* SCRIPT FILES ⚠️", "SKSE should be used for Skyrim instead of F4SE");
    }

    [Fact]
    public void AnalyzerProperties_AreSetCorrectly()
    {
        // Assert
        _analyzer.Name.Should().Be("Mod Conflict Analyzer", "analyzer name should be set correctly");
        _analyzer.Priority.Should().Be(50, "analyzer priority should be set correctly");
        _analyzer.CanRunInParallel.Should().BeTrue("analyzer should support parallel execution");
    }

    [Fact]
    public async Task AnalyzeAsync_WithCleanupFiles_ReportsAsWarning()
    {
        // Arrange
        await _appSettings.SaveSettingsAsync(new ApplicationSettings { FcxMode = true, ModsFolder = "C:\\TestMods" });
        
        _modScanner.AddIssue(new ModIssue
        {
            Type = ModIssueType.CleanupFile,
            FilePath = "Data\\desktop.ini",
            Description = "Cleanup file"
        });
        
        var crashLog = new CrashLog { FilePath = "test.log" };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        genericResult.HasFindings.Should().BeTrue("cleanup files should be detected");
        genericResult.ReportText.Should().Contain("🗑️ DETECTED UNINTENDED FILES 🗑️", "cleanup file warning should be displayed");
        genericResult.ReportText.Should().Contain("Data\\desktop.ini", "the cleanup file should be listed");
    }
}