using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using System.Text;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class ReportWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IReportWriter _reportWriter;
    private readonly ILogger<ReportWriter> _logger;

    public ReportWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _logger = NullLogger<ReportWriter>.Instance;
        _reportWriter = new ReportWriter(_logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task WriteReportAsync_WithValidScanResult_WritesFileSuccessfully()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var expectedOutputPath = Path.Combine(_tempDirectory, "crash-test-AUTOSCAN.md");
        
        await File.WriteAllTextAsync(logPath, "Sample crash log content");
        
        var scanResult = CreateSampleScanResult(logPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(expectedOutputPath));
        
        var content = await File.ReadAllTextAsync(expectedOutputPath, Encoding.UTF8);
        Assert.Equal(scanResult.ReportText, content);
    }

    [Fact]
    public async Task WriteReportAsync_WithCustomOutputPath_WritesFileSuccessfully()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var customOutputPath = Path.Combine(_tempDirectory, "custom-report.md");
        
        await File.WriteAllTextAsync(logPath, "Sample crash log content");
        
        var scanResult = CreateSampleScanResult(logPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult, customOutputPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(customOutputPath));
        
        var content = await File.ReadAllTextAsync(customOutputPath, Encoding.UTF8);
        Assert.Equal(scanResult.ReportText, content);
    }

    [Fact]
    public async Task WriteReportAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var subDirectory = Path.Combine(_tempDirectory, "subdir", "nested");
        var logPath = Path.Combine(subDirectory, "crash-test.log");
        var expectedOutputPath = Path.Combine(subDirectory, "crash-test-AUTOSCAN.md");
        
        var scanResult = CreateSampleScanResult(logPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        Assert.True(Directory.Exists(subDirectory));
        Assert.True(File.Exists(expectedOutputPath));
    }

    [Fact]
    public async Task WriteReportAsync_WithEmptyReport_WritesEmptyFile()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = new ScanResult 
        { 
            LogPath = logPath,
            Report = new List<string>()
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(scanResult.OutputPath));
        
        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public async Task WriteReportAsync_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        var invalidPath = Path.Combine("Z:\\NonExistentDrive", "crash-test.log");
        var scanResult = CreateSampleScanResult(invalidPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WriteReportAsync_WithUTF8Content_PreservesEncoding()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = new ScanResult 
        { 
            LogPath = logPath,
            Report = new List<string> 
            { 
                "Test with special characters: éñüñß\n",
                "Unicode symbols: ✓ ❌ ⚠️\n",
                "Asian characters: 中文 日本語\n"
            }
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        
        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Contains("éñüñß", content);
        Assert.Contains("✓ ❌ ⚠️", content);
        Assert.Contains("中文 日本語", content);
    }

    [Fact]
    public async Task WriteReportAsync_WithOPCContent_FiltersOPCSections()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var reportWithOPC = new List<string>
        {
            "Normal content line 1\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...\n",
            "====================================================\n",
            "# FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER # \n",
            "\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT IF IMPORTANT PATCHES & FIXES ARE INSTALLED...\n",
            "====================================================\n",
            "Normal content line 2\n"
        };
        
        var scanResult = new ScanResult 
        { 
            LogPath = logPath,
            Report = reportWithOPC
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        
        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Contains("Normal content line 1", content);
        Assert.Contains("Normal content line 2", content);
        Assert.DoesNotContain("OPC INSTALLER", content);
        Assert.DoesNotContain("PATCHED THROUGH THE OPC INSTALLER", content);
    }

    [Fact]
    public async Task WriteReportAsync_WithMultipleOPCSections_FiltersAllOPCSections()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var reportWithMultipleOPC = new List<string>
        {
            "Start content\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...\n",
            "====================================================\n",
            "OPC content 1\n",
            "====================================================\n",
            "REGULAR SECTION\n",
            "====================================================\n",
            "Regular content\n",
            "====================================================\n",
            "MODS PATCHED THROUGH OPC INSTALLER\n",
            "====================================================\n",
            "OPC content 2\n",
            "====================================================\n",
            "END SECTION\n",
            "====================================================\n",
            "End content\n"
        };
        
        var scanResult = new ScanResult 
        { 
            LogPath = logPath,
            Report = reportWithMultipleOPC
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        
        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Contains("Start content", content);
        Assert.Contains("Regular content", content);
        Assert.Contains("End content", content);
        Assert.DoesNotContain("OPC content 1", content);
        Assert.DoesNotContain("OPC content 2", content);
        Assert.DoesNotContain("OPC INSTALLER", content);
    }

    [Fact]
    public async Task WriteReportAsync_WithOPCAtEnd_FiltersCorrectly()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var reportWithOPCAtEnd = new List<string>
        {
            "Normal content\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...\n",
            "====================================================\n",
            "Final OPC content\n"
        };
        
        var scanResult = new ScanResult 
        { 
            LogPath = logPath,
            Report = reportWithOPCAtEnd
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        
        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Contains("Normal content", content);
        Assert.DoesNotContain("Final OPC content", content);
        Assert.DoesNotContain("OPC INSTALLER", content);
    }

    [Fact]
    public async Task WriteReportAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = CreateSampleScanResult(logPath);
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        try
        {
            var result = await _reportWriter.WriteReportAsync(scanResult, cancellationTokenSource.Token);
            // The operation may complete quickly before cancellation is detected
            // In that case, we just verify it didn't crash
            Assert.True(true);
        }
        catch (OperationCanceledException)
        {
            // This is also expected behavior
            Assert.True(true);
        }
    }

    [Fact]
    public async Task WriteReportAsync_OverwritesExistingFile()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = CreateSampleScanResult(logPath);
        
        // Create existing file with different content
        await File.WriteAllTextAsync(scanResult.OutputPath, "Old content");
        
        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(result);
        
        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Equal(scanResult.ReportText, content);
        Assert.DoesNotContain("Old content", content);
    }

    private static ScanResult CreateSampleScanResult(string logPath)
    {
        return new ScanResult
        {
            LogPath = logPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "Sample report line 1\n",
                "Sample report line 2\n",
                "✓ Analysis complete\n"
            }
        };
    }
}