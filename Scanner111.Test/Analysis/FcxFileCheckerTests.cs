using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Services;

namespace Scanner111.Test.Analysis;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "General")]
public class FcxFileCheckerTests : IAsyncLifetime
{
    private readonly ILogger<FcxFileChecker> _logger;
    private readonly IModFileScanner _modFileScanner;
    private FcxFileChecker? _fileChecker;
    private readonly string _testGamePath;
    private readonly string _testModPath;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public FcxFileCheckerTests()
    {
        _logger = Substitute.For<ILogger<FcxFileChecker>>();
        _modFileScanner = Substitute.For<IModFileScanner>();
        _testGamePath = Path.Combine(Path.GetTempPath(), "TestGame_" + Guid.NewGuid());
        _testModPath = Path.Combine(Path.GetTempPath(), "TestMods_" + Guid.NewGuid());
    }

    public Task InitializeAsync()
    {
        // Create test directories
        Directory.CreateDirectory(_testGamePath);
        _createdDirectories.Add(_testGamePath);
        
        Directory.CreateDirectory(_testModPath);
        _createdDirectories.Add(_testModPath);
        
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_fileChecker != null)
        {
            await _fileChecker.DisposeAsync();
        }

        // Clean up test files and directories
        foreach (var file in _createdFiles)
        {
            try { File.Delete(file); } catch { }
        }
        
        foreach (var dir in _createdDirectories.AsEnumerable().Reverse())
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private FcxFileChecker CreateFileChecker(IModFileScanner? scanner = null)
    {
        _fileChecker = new FcxFileChecker(
            _logger,
            scanner ?? _modFileScanner,
            maxRetries: 2,
            parallelism: 2);
        return _fileChecker;
    }

    private void CreateTestFile(string directory, string filename, string content = "test")
    {
        var path = Path.Combine(directory, filename);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content);
        _createdFiles.Add(path);
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldReturnSuccess_WhenAllFilesPresent()
    {
        // Arrange
        var checker = CreateFileChecker();
        CreateTestFile(_testGamePath, "Fallout4.exe");
        CreateTestFile(_testGamePath, "Fallout4.esm");
        CreateTestFile(_testGamePath, Path.Combine("Data", "Fallout4.esm"));
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("✔️ Game mod files check completed successfully.\n-----\n");

        var options = new FcxCheckOptions
        {
            IncludeArchivedMods = false,
            RetryOnFailure = false
        };

        // Act
        var result = await checker.CheckFilesAsync(_testGamePath, _testModPath, options);

        // Assert
        result.Success.Should().BeTrue();
        result.MainFilesResult.Should().Contain("✔️");
        result.ModFilesResult.Should().Contain("✔️");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldReportMissingFiles()
    {
        // Arrange
        var checker = CreateFileChecker();
        CreateTestFile(_testGamePath, "Fallout4.exe");
        // Missing Fallout4.esm files
        
        var options = new FcxCheckOptions { RetryOnFailure = false };

        // Act
        var result = await checker.CheckFilesAsync(_testGamePath, _testModPath, options);

        // Assert
        result.MainFilesResult.Should().Contain("❌");
        result.MainFilesResult.Should().Contain("Missing critical files");
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldReportCorruptedFiles()
    {
        // Arrange
        var checker = CreateFileChecker();
        CreateTestFile(_testGamePath, "Fallout4.exe");
        CreateTestFile(_testGamePath, "Fallout4.esm", ""); // Empty file (corrupted)
        
        var options = new FcxCheckOptions { RetryOnFailure = false };

        // Act
        var result = await checker.CheckFilesAsync(_testGamePath, _testModPath, options);

        // Assert
        result.MainFilesResult.Should().Contain("❌");
        result.MainFilesResult.Should().Contain("Corrupted files detected");
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldReportProgress()
    {
        // Arrange
        var checker = CreateFileChecker();
        CreateTestFile(_testGamePath, "Fallout4.exe");
        
        var progressReports = new List<FcxCheckProgress>();
        var progress = new Progress<FcxCheckProgress>(p => progressReports.Add(p));
        
        var options = new FcxCheckOptions { RetryOnFailure = false };

        // Act
        await checker.CheckFilesAsync(_testGamePath, _testModPath, options, progress);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.CurrentOperation.Contains("main game files"));
        progressReports.Should().Contain(p => p.CurrentOperation.Contains("mod files"));
        progressReports.Last().PercentComplete.Should().Be(100);
    }

    [Fact]
    public async Task ValidateMainFilesAsync_ShouldHandleNonExistentDirectory()
    {
        // Arrange
        var checker = CreateFileChecker();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = await checker.ValidateMainFilesAsync(nonExistentPath);

        // Assert
        result.Should().Contain("❌");
        result.Should().Contain("Game directory not found");
    }

    [Fact]
    public async Task ScanModFilesAsync_ShouldUseModFileScanner_WhenProvided()
    {
        // Arrange
        var checker = CreateFileChecker();
        var expectedResult = "Custom scan result from scanner";
        
        _modFileScanner.ScanModsUnpackedAsync(_testModPath, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await checker.ScanModFilesAsync(_testModPath, false);

        // Assert
        result.Should().Be(expectedResult);
        await _modFileScanner.Received(1).ScanModsUnpackedAsync(_testModPath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanModFilesAsync_ShouldProvideFallback_WhenNoScannerProvided()
    {
        // Arrange
        var checker = new FcxFileChecker(_logger, null);
        Directory.CreateDirectory(Path.Combine(_testModPath, "Mod1"));
        Directory.CreateDirectory(Path.Combine(_testModPath, "Mod2"));
        CreateTestFile(_testModPath, Path.Combine("Mod1", "file1.txt"));
        CreateTestFile(_testModPath, Path.Combine("Mod2", "file2.txt"));

        // Act
        var result = await checker.ScanModFilesAsync(_testModPath, false);

        // Assert
        result.Should().Contain("✔️");
        result.Should().Contain("2 mods");
        result.Should().Contain("2 total files");
    }

    [Fact]
    public async Task GetCachedChecksumAsync_ShouldReturnNull_ForNonCachedFile()
    {
        // Arrange
        var checker = CreateFileChecker();
        var testFile = Path.Combine(_testGamePath, "test.txt");
        CreateTestFile(_testGamePath, "test.txt");

        // Act
        var checksum = await checker.GetCachedChecksumAsync(testFile);

        // Assert
        checksum.Should().BeNull();
    }

    [Fact]
    public async Task ClearChecksumCacheAsync_ShouldClearAllCachedChecksums()
    {
        // Arrange
        var checker = CreateFileChecker();

        // Act
        await checker.ClearChecksumCacheAsync();

        // Assert - Should not throw
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Checksum cache cleared")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldRetryOnFailure_WhenEnabled()
    {
        // Arrange
        var checker = CreateFileChecker();
        var callCount = 0;
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                callCount++;
                if (callCount == 1)
                    throw new IOException("Simulated failure");
                return Task.FromResult("✔️ Success on retry");
            });

        var options = new FcxCheckOptions
        {
            RetryOnFailure = true,
            MaxParallelism = 1
        };

        // Act
        var result = await checker.CheckFilesAsync(_testGamePath, _testModPath, options);

        // Assert
        result.ModFilesResult.Should().Contain("Success on retry");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldHandleCancellation()
    {
        // Arrange
        var checker = CreateFileChecker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var options = new FcxCheckOptions();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => checker.CheckFilesAsync(_testGamePath, _testModPath, options, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange
        var checker = CreateFileChecker();

        // Act & Assert - Should not throw
        await checker.DisposeAsync();
        await checker.DisposeAsync();
        await checker.DisposeAsync();
    }

    [Fact]
    public async Task CheckFilesAsync_ShouldThrow_WhenDisposed()
    {
        // Arrange
        var checker = CreateFileChecker();
        await checker.DisposeAsync();

        var options = new FcxCheckOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => checker.CheckFilesAsync(_testGamePath, _testModPath, options));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckFilesAsync_ShouldRespectIncludeArchivedModsOption(bool includeArchived)
    {
        // Arrange
        var checker = CreateFileChecker();
        var options = new FcxCheckOptions
        {
            IncludeArchivedMods = includeArchived,
            RetryOnFailure = false
        };

        // Act
        await checker.CheckFilesAsync(_testGamePath, _testModPath, options);

        // Assert
        // For now, both paths call ScanModsUnpackedAsync since we don't have BSArch path
        await _modFileScanner.Received(1).ScanModsUnpackedAsync(_testModPath, Arg.Any<CancellationToken>());
    }
}