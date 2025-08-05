using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.FCX;
using Scanner111.Core.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Tests.TestHelpers;
using Xunit;

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
        Assert.True(genericResult.Success);
        Assert.True(genericResult.HasFindings);
        Assert.Contains("⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️", genericResult.ReportText);
        Assert.Contains("Data\\Textures\\test.dds (1023x1023)", genericResult.ReportText);
        Assert.Contains("Texture (DDS) Crash", genericResult.ReportText);
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
        Assert.True(genericResult.HasFindings);
        Assert.Contains("❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓", genericResult.ReportText);
        Assert.Contains("Data\\Textures\\badformat.png", genericResult.ReportText);
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
        Assert.True(genericResult.HasFindings);
        Assert.Contains("⚠️ MODS CONTAIN COPIES OF *F4SE* SCRIPT FILES ⚠️", genericResult.ReportText);
        Assert.Contains("Data\\F4SE\\Plugins\\test.dll", genericResult.ReportText);
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
        Assert.True(genericResult.HasFindings);
        Assert.Contains("🔧 PREVIS FILES DETECTED 🔧", genericResult.ReportText);
        Assert.Contains("should load after the PRP.esp plugin", genericResult.ReportText);
        Assert.Contains("Data\\Meshes\\Precombined\\test.nif", genericResult.ReportText);
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
        Assert.True(genericResult.HasFindings);
        
        // Each issue type should have its own section
        Assert.Contains("⚠️ DDS DIMENSIONS", genericResult.ReportText);
        Assert.Contains("❓ SOUND FILES HAVE INCORRECT FORMAT", genericResult.ReportText);
        Assert.Contains("💀 BROKEN ANIMATION DATA FILES 💀", genericResult.ReportText);
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
        Assert.True(genericResult.Success);
        Assert.False(genericResult.HasFindings);
        Assert.Empty(genericResult.ReportLines);
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
        Assert.True(genericResult.Success);
        Assert.False(genericResult.HasFindings);
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
        Assert.Contains("⚠️ MODS CONTAIN COPIES OF *SKSE* SCRIPT FILES ⚠️", genericResult.ReportText);
    }

    [Fact]
    public void AnalyzerProperties_AreSetCorrectly()
    {
        // Assert
        Assert.Equal("Mod Conflict Analyzer", _analyzer.Name);
        Assert.Equal(50, _analyzer.Priority);
        Assert.True(_analyzer.CanRunInParallel);
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
        Assert.True(genericResult.HasFindings);
        Assert.Contains("🗑️ DETECTED UNINTENDED FILES 🗑️", genericResult.ReportText);
        Assert.Contains("Data\\desktop.ini", genericResult.ReportText);
    }
}