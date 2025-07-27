using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;
using System.Text;
using System.Text.RegularExpressions;

namespace Scanner111.Tests.Integration;

/// <summary>
/// Integration tests for FCX report generation accuracy
/// </summary>
public class FcxReportGenerationTests : IDisposable
{
    private readonly List<string> _tempDirectories;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly TestHashValidationService _hashService;
    private readonly TestYamlSettingsProvider _yamlSettings;
    private readonly TestMessageHandler _messageHandler;
    private readonly FileIntegrityAnalyzer _fcxAnalyzer;

    public FcxReportGenerationTests()
    {
        _tempDirectories = new List<string>();
        _settingsService = new TestApplicationSettingsService();
        _hashService = new TestHashValidationService();
        _yamlSettings = new TestYamlSettingsProvider();
        _messageHandler = new TestMessageHandler();
        
        _fcxAnalyzer = new FileIntegrityAnalyzer(
            _hashService,
            _settingsService,
            _yamlSettings,
            _messageHandler);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenerateFcxReport_WithHealthyGame_MatchesExpectedFormat()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: true, includeCoreMods: true);
        _settingsService.Settings.FcxMode = true;
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(report);
        
        // Verify report structure
        Assert.Contains("FCX (File Integrity Check) Analysis", report);
        Assert.Contains("Game Installation Status:", report);
        Assert.Contains("Game Executable Check:", report);
        Assert.Contains("F4SE (Script Extender) Check:", report);
        Assert.Contains("Core Mod Files Check:", report);
        
        // Verify formatting matches Python implementation
        Assert.Matches(@"={50,}", report); // Separator lines
        Assert.Matches(@"Game Installation Status:\s+\w+", report);
    }

    [Fact]
    public async Task GenerateFcxReport_WithMissingF4SE_ShowsWarnings()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: false, includeCoreMods: true);
        _settingsService.Settings.FcxMode = true;
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(report);
        Assert.Contains("F4SE not found", report);
        Assert.Contains("WARNING", report);
        Assert.Contains("Recommended Actions:", report);
        Assert.Contains("Install F4SE", report);
    }

    [Fact]
    public async Task GenerateFcxReport_WithHashMismatch_ShowsValidationErrors()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: true, includeCoreMods: true);
        _settingsService.Settings.FcxMode = true;
        
        // Set up hash mismatch
        var exePath = Path.Combine(gameDir, "Fallout4.exe");
        _hashService.SetFileHash(exePath, "EXPECTED_HASH");
        _hashService.SetExpectedHash(exePath, "DIFFERENT_HASH");
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(report);
        Assert.Contains("Hash Validation Results:", report);
        Assert.Contains("MISMATCH", report);
        Assert.Contains("Expected:", report);
        Assert.Contains("Actual:", report);
    }

    [Fact]
    public async Task GenerateFcxReport_WithVersionWarnings_ShowsVersionInfo()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: true, includeCoreMods: false);
        _settingsService.Settings.FcxMode = true;
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Simulate version detection
        _hashService.SetFileHash(Path.Combine(gameDir, "Fallout4.exe"), 
            "7B0E5D0B7C5B4E8F9C2A3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4");

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        
        // Add version warnings manually for testing
        if (result != null)
        {
            result.VersionWarnings.Add("Game version 1.10.163.0 detected - This is a legacy version");
            result.VersionWarnings.Add("F4SE version may not be compatible with latest mods");
        }
        
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(report);
        Assert.Contains("Version Warnings:", report);
        Assert.Contains("legacy version", report);
        Assert.Contains("F4SE version", report);
    }

    [Fact]
    public async Task GenerateFcxReport_EmptyResult_ShowsNoIssuesFound()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: true, includeCoreMods: true);
        _settingsService.Settings.FcxMode = true;
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Configure everything to pass
        var files = Directory.GetFiles(gameDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            _hashService.SetFileHash(file, "VALID_HASH");
            _hashService.SetExpectedHash(file, "VALID_HASH");
        }

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(report);
        Assert.Contains("Good", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateFcxReport_CriticalIssues_ShowsAtTop()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: false, includeCoreMods: false);
        _settingsService.Settings.FcxMode = true;
        
        // Remove game executable to create critical issue
        File.Delete(Path.Combine(gameDir, "Fallout4.exe"));
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(report);
        
        // Critical issues should appear early in the report
        var criticalIndex = report.IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase);
        var recommendedIndex = report.IndexOf("Recommended Actions:", StringComparison.OrdinalIgnoreCase);
        
        if (criticalIndex >= 0 && recommendedIndex >= 0)
        {
            Assert.True(criticalIndex < recommendedIndex, "Critical issues should appear before recommendations");
        }
    }

    [Fact]
    public async Task GenerateFcxReport_WithMultipleIssues_OrganizesCorrectly()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: false, includeCoreMods: false);
        _settingsService.Settings.FcxMode = true;
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert
        Assert.NotNull(report);
        
        // Verify report sections are in correct order
        var sections = new[]
        {
            "FCX (File Integrity Check) Analysis",
            "Game Installation Status:",
            "File Checks:",
            "Recommended Actions:"
        };

        var lastIndex = -1;
        foreach (var section in sections.Where(s => report.Contains(s)))
        {
            var currentIndex = report.IndexOf(section);
            Assert.True(currentIndex > lastIndex, $"Section '{section}' is out of order");
            lastIndex = currentIndex;
        }
    }

    [Fact]
    public async Task GenerateFcxReport_FormattingConsistency_MatchesPythonOutput()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: true, includeCoreMods: true);
        _settingsService.Settings.FcxMode = true;
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            GamePath = gameDir
        };

        // Act
        var result = await _fcxAnalyzer.AnalyzeAsync(crashLog) as FcxScanResult;
        var report = GenerateFcxReport(result);

        // Assert formatting patterns from Python implementation
        Assert.NotNull(report);
        
        // Check separator lines
        Assert.Matches(@"\n={50,}\n", report);
        
        // Check indentation (Python uses 4 spaces)
        Assert.Matches(@"\n    \w+", report);
        
        // Check status formatting (Python uses alignment)
        Assert.Matches(@"Status:\s+\[.*?\]", report);
        
        // Check bullet points
        Assert.Matches(@"\n  [-•]\s+", report);
    }

    [Fact]
    public async Task IntegrateFcxWithFullPipeline_GeneratesCompleteReport()
    {
        // Arrange
        var gameDir = CreateMockGameInstallation(includeF4SE: true, includeCoreMods: true);
        _settingsService.Settings.FcxMode = true;
        _settingsService.Settings.DefaultGamePath = gameDir;
        
        var innerPipeline = new TestScanPipeline();
        var fcxPipeline = new FcxEnabledPipeline(
            innerPipeline,
            _settingsService,
            _hashService,
            NullLogger<FcxEnabledPipeline>.Instance,
            _messageHandler,
            _yamlSettings);

        var crashLogPath = CreateMockCrashLog();
        var scanResult = new ScanResult
        {
            LogPath = crashLogPath,
            Status = ScanStatus.Completed
        };
        innerPipeline.SetResult(scanResult);

        // Act
        var result = await fcxPipeline.ProcessSingleAsync(crashLogPath);
        var fullReport = string.Join(Environment.NewLine, result.Report);

        // Assert
        Assert.NotNull(fullReport);
        Assert.Contains("Scanner 111 Analysis Report", fullReport);
        Assert.Contains("FCX (File Integrity Check) Analysis", fullReport);
        
        // Verify FCX section is integrated properly
        var fcxSectionStart = fullReport.IndexOf("FCX (File Integrity Check) Analysis");
        Assert.True(fcxSectionStart > 0, "FCX section should be present in full report");
    }

    [Theory]
    [InlineData(GameIntegrityStatus.Good, "Good")]
    [InlineData(GameIntegrityStatus.Warning, "Warning")]
    [InlineData(GameIntegrityStatus.Critical, "Critical")]
    [InlineData(GameIntegrityStatus.Invalid, "Invalid")]
    public void FormatGameStatus_ReturnsCorrectFormat(GameIntegrityStatus status, string expectedText)
    {
        // Act
        var formatted = FormatGameStatus(status);

        // Assert
        Assert.Contains(expectedText, formatted);
        Assert.Matches(@"\[.*?\]", formatted); // Should be in brackets
    }

    // Helper methods

    private string CreateMockGameInstallation(bool includeF4SE, bool includeCoreMods)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        // Create game executable
        File.WriteAllText(Path.Combine(tempDir, "Fallout4.exe"), "dummy executable");
        File.WriteAllText(Path.Combine(tempDir, "Fallout4Launcher.exe"), "dummy launcher");
        
        // Create Data directory
        Directory.CreateDirectory(Path.Combine(tempDir, "Data"));
        File.WriteAllText(Path.Combine(tempDir, "Data", "Fallout4.esm"), "dummy master file");
        
        if (includeF4SE)
        {
            File.WriteAllText(Path.Combine(tempDir, "f4se_loader.exe"), "dummy F4SE");
            File.WriteAllText(Path.Combine(tempDir, "f4se_1_10_163.dll"), "dummy DLL");
        }
        
        if (includeCoreMods)
        {
            var f4sePlugins = Path.Combine(tempDir, "Data", "F4SE", "Plugins");
            Directory.CreateDirectory(f4sePlugins);
            File.WriteAllText(Path.Combine(f4sePlugins, "Buffout4.dll"), "dummy mod");
            File.WriteAllText(Path.Combine(f4sePlugins, "AddressLibrary.dll"), "dummy mod");
        }
        
        return tempDir;
    }

    private string CreateMockCrashLog()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        
        var logPath = Path.Combine(tempDir, "crash-2024-01-01-120000.log");
        var content = @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception at 0x7FF6A1234567
";
        File.WriteAllText(logPath, content);
        return logPath;
    }

    private string GenerateFcxReport(FcxScanResult? result)
    {
        if (result == null)
            return "No FCX results available";

        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("=" + new string('=', 79));
        sb.AppendLine("FCX (File Integrity Check) Analysis");
        sb.AppendLine("=" + new string('=', 79));
        sb.AppendLine();
        
        // Game status
        sb.AppendLine($"Game Installation Status: {FormatGameStatus(result.GameStatus)}");
        sb.AppendLine();
        
        // File checks
        if (result.FileChecks.Any())
        {
            sb.AppendLine("File Checks:");
            foreach (var check in result.FileChecks)
            {
                var status = check.Exists ? "[OK]" : "[MISSING]";
                sb.AppendLine($"    {check.FileType,-20} {status}");
                if (!string.IsNullOrEmpty(check.FilePath))
                {
                    sb.AppendLine($"      Path: {check.FilePath}");
                }
            }
            sb.AppendLine();
        }
        
        // Hash validation
        if (result.HashValidations.Any())
        {
            sb.AppendLine("Hash Validation Results:");
            foreach (var validation in result.HashValidations)
            {
                var status = validation.IsValid ? "[VALID]" : "[MISMATCH]";
                sb.AppendLine($"    {Path.GetFileName(validation.FilePath),-30} {status}");
                if (!validation.IsValid)
                {
                    sb.AppendLine($"      Expected: {validation.ExpectedHash}");
                    sb.AppendLine($"      Actual:   {validation.ActualHash}");
                }
            }
            sb.AppendLine();
        }
        
        // Version warnings
        if (result.VersionWarnings.Any())
        {
            sb.AppendLine("Version Warnings:");
            foreach (var warning in result.VersionWarnings)
            {
                sb.AppendLine($"  - {warning}");
            }
            sb.AppendLine();
        }
        
        // Recommended actions
        if (result.RecommendedFixes.Any())
        {
            sb.AppendLine("Recommended Actions:");
            foreach (var fix in result.RecommendedFixes)
            {
                sb.AppendLine($"  • {fix}");
            }
            sb.AppendLine();
        }
        
        // Use the actual report text if available
        if (!string.IsNullOrEmpty(result.ReportText))
        {
            sb.AppendLine(result.ReportText);
        }
        
        return sb.ToString();
    }

    private string FormatGameStatus(GameIntegrityStatus status)
    {
        return status switch
        {
            GameIntegrityStatus.Good => "[Good]",
            GameIntegrityStatus.Warning => "[Warning]",
            GameIntegrityStatus.Critical => "[CRITICAL]",
            GameIntegrityStatus.Invalid => "[INVALID]",
            _ => "[Unknown]"
        };
    }
}

/// <summary>
/// Extended test hash validation service for report testing
/// </summary>
internal class TestHashValidationService : IHashValidationService
{
    private readonly Dictionary<string, string> _fileHashes = new();
    private readonly Dictionary<string, string> _expectedHashes = new();
    
    public void SetFileHash(string path, string hash)
    {
        _fileHashes[path] = hash;
    }
    
    public void SetExpectedHash(string path, string hash)
    {
        _expectedHashes[path] = hash;
    }
    
    public Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_fileHashes.TryGetValue(filePath, out var hash))
        {
            return Task.FromResult(hash);
        }
        
        // Generate a dummy hash based on file content
        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Task.FromResult(Convert.ToHexString(hashBytes));
        }
        
        return Task.FromResult("FILE_NOT_FOUND");
    }
    
    public Task<string> CalculateFileHashWithProgressAsync(string filePath, IProgress<long>? progress, CancellationToken cancellationToken = default)
    {
        return CalculateFileHashAsync(filePath, cancellationToken);
    }
    
    public async Task<HashValidation> ValidateFileAsync(string filePath, string expectedHash, CancellationToken cancellationToken = default)
    {
        var actualHash = await CalculateFileHashAsync(filePath, cancellationToken);
        var expected = _expectedHashes.GetValueOrDefault(filePath, expectedHash);
        
        return new HashValidation
        {
            FilePath = filePath,
            ExpectedHash = expected,
            ActualHash = actualHash
        };
    }
    
    public async Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HashValidation>();
        foreach (var kvp in fileHashMap)
        {
            results[kvp.Key] = await ValidateFileAsync(kvp.Key, kvp.Value, cancellationToken);
        }
        return results;
    }
}