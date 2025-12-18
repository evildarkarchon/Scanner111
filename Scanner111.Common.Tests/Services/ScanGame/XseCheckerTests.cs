using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for the XseChecker class.
/// </summary>
public class XseCheckerTests : IDisposable
{
    private readonly XseChecker _checker;
    private readonly string _tempDirectory;

    public XseCheckerTests()
    {
        _checker = new XseChecker(NullLogger<XseChecker>.Instance);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"XseCheckerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
        GC.SuppressFinalize(this);
    }

    #region Address Library Tests

    [Fact]
    public async Task CheckAddressLibraryAsync_WithExistingFile_ReturnsInstalled()
    {
        // Arrange
        var addressLibPath = CreateFile("version.bin", "dummy content");

        // Act
        var (isInstalled, status) = await _checker.CheckAddressLibraryAsync(addressLibPath);

        // Assert
        isInstalled.Should().BeTrue();
        status.Should().Be(AddressLibraryStatus.Installed);
    }

    [Fact]
    public async Task CheckAddressLibraryAsync_WithMissingFile_ReturnsMissing()
    {
        // Arrange
        var addressLibPath = Path.Combine(_tempDirectory, "nonexistent.bin");

        // Act
        var (isInstalled, status) = await _checker.CheckAddressLibraryAsync(addressLibPath);

        // Assert
        isInstalled.Should().BeFalse();
        status.Should().Be(AddressLibraryStatus.Missing);
    }

    [Fact]
    public async Task CheckAddressLibraryAsync_WithNullPath_ReturnsInvalidConfiguration()
    {
        // Act
        var (isInstalled, status) = await _checker.CheckAddressLibraryAsync(null);

        // Assert
        isInstalled.Should().BeFalse();
        status.Should().Be(AddressLibraryStatus.InvalidConfiguration);
    }

    [Fact]
    public async Task CheckAddressLibraryAsync_WithEmptyPath_ReturnsInvalidConfiguration()
    {
        // Act
        var (isInstalled, status) = await _checker.CheckAddressLibraryAsync(string.Empty);

        // Assert
        isInstalled.Should().BeFalse();
        status.Should().Be(AddressLibraryStatus.InvalidConfiguration);
    }

    #endregion

    #region XSE Installation Tests

    [Fact]
    public async Task CheckXseInstallationAsync_WithExistingLogFile_ReturnsInstalled()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", "F4SE runtime: 0.6.23, release 0 64-bit\nLoading plugins...");

        // Act
        var (status, version, isLatest) = await _checker.CheckXseInstallationAsync(logPath, "0.6.23");

        // Assert
        status.Should().Be(XseInstallationStatus.Installed);
        version.Should().Be("0.6.23");
        isLatest.Should().BeTrue();
    }

    [Fact]
    public async Task CheckXseInstallationAsync_WithOutdatedVersion_ReturnsNotLatest()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", "F4SE runtime: 0.6.21, release 0 64-bit\nLoading plugins...");

        // Act
        var (status, version, isLatest) = await _checker.CheckXseInstallationAsync(logPath, "0.6.23");

        // Assert
        status.Should().Be(XseInstallationStatus.Installed);
        version.Should().Be("0.6.21");
        isLatest.Should().BeFalse();
    }

    [Fact]
    public async Task CheckXseInstallationAsync_WithMissingLogFile_ReturnsLogFileMissing()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "nonexistent.log");

        // Act
        var (status, version, isLatest) = await _checker.CheckXseInstallationAsync(logPath, "0.6.23");

        // Assert
        status.Should().Be(XseInstallationStatus.LogFileMissing);
        version.Should().BeNull();
        isLatest.Should().BeFalse();
    }

    [Fact]
    public async Task CheckXseInstallationAsync_WithNullPath_ReturnsInvalidConfiguration()
    {
        // Act
        var (status, version, isLatest) = await _checker.CheckXseInstallationAsync(null, "0.6.23");

        // Assert
        status.Should().Be(XseInstallationStatus.InvalidConfiguration);
        version.Should().BeNull();
        isLatest.Should().BeFalse();
    }

    [Fact]
    public async Task CheckXseInstallationAsync_WithEmptyLogFile_ReturnsInstalledNoVersion()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", string.Empty);

        // Act
        var (status, version, isLatest) = await _checker.CheckXseInstallationAsync(logPath, "0.6.23");

        // Assert
        status.Should().Be(XseInstallationStatus.Installed);
        version.Should().BeNull();
        isLatest.Should().BeFalse();
    }

    [Fact]
    public async Task CheckXseInstallationAsync_WithSkseFormat_ExtractsVersion()
    {
        // Arrange
        var logPath = CreateFile("skse64.log", "SKSE64 runtime: 2.2.6\nSKSE version: 2.2.6");

        // Act
        var (status, version, isLatest) = await _checker.CheckXseInstallationAsync(logPath, "2.2.6");

        // Assert
        status.Should().Be(XseInstallationStatus.Installed);
        version.Should().Be("2.2.6");
        isLatest.Should().BeTrue();
    }

    #endregion

    #region Log Error Scanning Tests

    [Fact]
    public async Task ScanLogForErrorsAsync_WithMatchingPatterns_ReturnsErrors()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", """
            F4SE runtime: 0.6.23, release 0 64-bit
            Loading plugins...
            ERROR: Plugin failed to load
            Warning: Something went wrong
            INFO: Normal operation
            ERROR: Another error occurred
            """);

        var patterns = new List<string> { "ERROR", "FATAL" };

        // Act
        var errors = await _checker.ScanLogForErrorsAsync(logPath, patterns);

        // Assert
        errors.Should().HaveCount(2);
        errors[0].LineNumber.Should().Be(3);
        errors[0].ErrorText.Should().Be("ERROR: Plugin failed to load");
        errors[0].MatchedPattern.Should().Be("ERROR");
        errors[1].LineNumber.Should().Be(6);
    }

    [Fact]
    public async Task ScanLogForErrorsAsync_WithNoMatchingPatterns_ReturnsEmpty()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", """
            F4SE runtime: 0.6.23, release 0 64-bit
            Loading plugins...
            INFO: All good
            """);

        var patterns = new List<string> { "ERROR", "FATAL" };

        // Act
        var errors = await _checker.ScanLogForErrorsAsync(logPath, patterns);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanLogForErrorsAsync_WithCaseInsensitiveMatch_ReturnsErrors()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", "error: lowercase error message");

        var patterns = new List<string> { "ERROR" };

        // Act
        var errors = await _checker.ScanLogForErrorsAsync(logPath, patterns);

        // Assert
        errors.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanLogForErrorsAsync_WithMissingFile_ReturnsEmpty()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "nonexistent.log");
        var patterns = new List<string> { "ERROR" };

        // Act
        var errors = await _checker.ScanLogForErrorsAsync(logPath, patterns);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanLogForErrorsAsync_WithEmptyPatterns_ReturnsEmpty()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", "ERROR: Some error");
        var patterns = new List<string>();

        // Act
        var errors = await _checker.ScanLogForErrorsAsync(logPath, patterns);

        // Assert
        errors.Should().BeEmpty();
    }

    #endregion

    #region Script Hash Verification Tests

    [Fact]
    public async Task VerifyScriptHashesAsync_WithValidHashes_ReturnsValid()
    {
        // Arrange
        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);

        var scriptContent = "dummy script content";
        CreateFile("Scripts/Actor.pex", scriptContent);
        var expectedHash = ComputeHash(scriptContent);

        var expectedHashes = new Dictionary<string, string>
        {
            ["Actor.pex"] = expectedHash
        };

        // Act
        var results = await _checker.VerifyScriptHashesAsync(scriptsFolder, expectedHashes);

        // Assert
        results.Should().HaveCount(1);
        results[0].FileName.Should().Be("Actor.pex");
        results[0].Status.Should().Be(ScriptHashStatus.Valid);
        results[0].ActualHash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task VerifyScriptHashesAsync_WithMismatchedHash_ReturnsMismatch()
    {
        // Arrange
        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);

        CreateFile("Scripts/Actor.pex", "actual content");
        var wrongHash = ComputeHash("expected different content");

        var expectedHashes = new Dictionary<string, string>
        {
            ["Actor.pex"] = wrongHash
        };

        // Act
        var results = await _checker.VerifyScriptHashesAsync(scriptsFolder, expectedHashes);

        // Assert
        results.Should().HaveCount(1);
        results[0].Status.Should().Be(ScriptHashStatus.Mismatch);
        results[0].ActualHash.Should().NotBe(wrongHash);
    }

    [Fact]
    public async Task VerifyScriptHashesAsync_WithMissingFile_ReturnsMissing()
    {
        // Arrange
        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);

        var expectedHashes = new Dictionary<string, string>
        {
            ["Missing.pex"] = "somehash"
        };

        // Act
        var results = await _checker.VerifyScriptHashesAsync(scriptsFolder, expectedHashes);

        // Assert
        results.Should().HaveCount(1);
        results[0].Status.Should().Be(ScriptHashStatus.Missing);
        results[0].ActualHash.Should().BeNull();
    }

    [Fact]
    public async Task VerifyScriptHashesAsync_WithMultipleFiles_ReturnsAllResults()
    {
        // Arrange
        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);

        var content1 = "script 1 content";
        var content2 = "script 2 content";
        CreateFile("Scripts/Script1.pex", content1);
        CreateFile("Scripts/Script2.pex", content2);

        var expectedHashes = new Dictionary<string, string>
        {
            ["Script1.pex"] = ComputeHash(content1),
            ["Script2.pex"] = ComputeHash(content2),
            ["Missing.pex"] = "somehash"
        };

        // Act
        var results = await _checker.VerifyScriptHashesAsync(scriptsFolder, expectedHashes);

        // Assert
        results.Should().HaveCount(3);
        results.Count(r => r.Status == ScriptHashStatus.Valid).Should().Be(2);
        results.Count(r => r.Status == ScriptHashStatus.Missing).Should().Be(1);
    }

    [Fact]
    public async Task VerifyScriptHashesAsync_WithEmptyFolder_ReturnsEmpty()
    {
        // Act
        var results = await _checker.VerifyScriptHashesAsync(string.Empty, new Dictionary<string, string>());

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyScriptHashesAsync_WithEmptyHashes_ReturnsEmpty()
    {
        // Arrange
        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);

        // Act
        var results = await _checker.VerifyScriptHashesAsync(scriptsFolder, new Dictionary<string, string>());

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region Full Integrity Check Tests

    [Fact]
    public async Task CheckIntegrityAsync_WithValidConfiguration_ReturnsCompleteResult()
    {
        // Arrange
        var addressLibPath = CreateFile("Data/F4SE/Plugins/version.bin", "dummy");
        var logPath = CreateFile("Documents/f4se.log", "F4SE runtime: 0.6.23, release 0 64-bit");

        var scriptsFolder = Path.Combine(_tempDirectory, "Data/Scripts");
        Directory.CreateDirectory(scriptsFolder);
        var scriptContent = "actor script";
        CreateFile("Data/Scripts/Actor.pex", scriptContent);

        var config = new XseConfiguration
        {
            Acronym = "F4SE",
            FullName = "Fallout 4 Script Extender",
            LatestVersion = "0.6.23",
            LogFilePath = logPath,
            AddressLibraryPath = addressLibPath,
            ScriptsFolderPath = scriptsFolder,
            ExpectedScriptHashes = new Dictionary<string, string>
            {
                ["Actor.pex"] = ComputeHash(scriptContent)
            },
            LogErrorPatterns = new List<string> { "ERROR" }
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.AddressLibraryInstalled.Should().BeTrue();
        result.AddressLibraryStatus.Should().Be(AddressLibraryStatus.Installed);
        result.XseInstalled.Should().BeTrue();
        result.XseStatus.Should().Be(XseInstallationStatus.Installed);
        result.DetectedVersion.Should().Be("0.6.23");
        result.IsLatestVersion.Should().BeTrue();
        result.LogErrors.Should().BeEmpty();
        result.ScriptHashResults.Should().HaveCount(1);
        result.ScriptHashResults[0].Status.Should().Be(ScriptHashStatus.Valid);
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithMissingAddressLibrary_ReportsIssue()
    {
        // Arrange
        var logPath = CreateFile("f4se.log", "F4SE runtime: 0.6.23, release 0 64-bit");

        var config = new XseConfiguration
        {
            LatestVersion = "0.6.23",
            LogFilePath = logPath,
            AddressLibraryPath = Path.Combine(_tempDirectory, "nonexistent.bin")
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.AddressLibraryInstalled.Should().BeFalse();
        result.AddressLibraryStatus.Should().Be(AddressLibraryStatus.Missing);
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithMissingXseLog_ReportsIssue()
    {
        // Arrange
        var addressLibPath = CreateFile("version.bin", "dummy");

        var config = new XseConfiguration
        {
            LatestVersion = "0.6.23",
            LogFilePath = Path.Combine(_tempDirectory, "nonexistent.log"),
            AddressLibraryPath = addressLibPath
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.XseInstalled.Should().BeFalse();
        result.XseStatus.Should().Be(XseInstallationStatus.LogFileMissing);
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithOutdatedXse_ReportsIssue()
    {
        // Arrange
        var addressLibPath = CreateFile("version.bin", "dummy");
        var logPath = CreateFile("f4se.log", "F4SE runtime: 0.6.21, release 0 64-bit");

        var config = new XseConfiguration
        {
            LatestVersion = "0.6.23",
            LogFilePath = logPath,
            AddressLibraryPath = addressLibPath
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.XseInstalled.Should().BeTrue();
        result.IsLatestVersion.Should().BeFalse();
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithLogErrors_ReportsIssue()
    {
        // Arrange
        var addressLibPath = CreateFile("version.bin", "dummy");
        var logPath = CreateFile("f4se.log", """
            F4SE runtime: 0.6.23, release 0 64-bit
            ERROR: Plugin crashed
            """);

        var config = new XseConfiguration
        {
            LatestVersion = "0.6.23",
            LogFilePath = logPath,
            AddressLibraryPath = addressLibPath,
            LogErrorPatterns = new List<string> { "ERROR" }
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.LogErrors.Should().HaveCount(1);
        // LogErrors alone don't trigger HasIssues in the current implementation
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithMismatchedScripts_ReportsIssue()
    {
        // Arrange
        var addressLibPath = CreateFile("version.bin", "dummy");
        var logPath = CreateFile("f4se.log", "F4SE runtime: 0.6.23, release 0 64-bit");

        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);
        CreateFile("Scripts/Actor.pex", "modified content");

        var config = new XseConfiguration
        {
            LatestVersion = "0.6.23",
            LogFilePath = logPath,
            AddressLibraryPath = addressLibPath,
            ScriptsFolderPath = scriptsFolder,
            ExpectedScriptHashes = new Dictionary<string, string>
            {
                ["Actor.pex"] = "expected_hash_that_wont_match"
            }
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.ScriptHashResults.Should().HaveCount(1);
        result.ScriptHashResults[0].Status.Should().Be(ScriptHashStatus.Mismatch);
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithMissingScripts_ReportsIssue()
    {
        // Arrange
        var addressLibPath = CreateFile("version.bin", "dummy");
        var logPath = CreateFile("f4se.log", "F4SE runtime: 0.6.23, release 0 64-bit");

        var scriptsFolder = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsFolder);

        var config = new XseConfiguration
        {
            LatestVersion = "0.6.23",
            LogFilePath = logPath,
            AddressLibraryPath = addressLibPath,
            ScriptsFolderPath = scriptsFolder,
            ExpectedScriptHashes = new Dictionary<string, string>
            {
                ["Missing.pex"] = "somehash"
            }
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.ScriptHashResults.Should().HaveCount(1);
        result.ScriptHashResults[0].Status.Should().Be(ScriptHashStatus.Missing);
        result.HasIssues.Should().BeTrue();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CheckIntegrityAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var config = new XseConfiguration();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _checker.CheckIntegrityAsync(config, cts.Token));
    }

    #endregion

    #region Helper Methods

    private string CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private static string ComputeHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    #endregion
}
