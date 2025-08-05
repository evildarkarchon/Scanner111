using System.Collections.Generic;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Core.FCX;
using Scanner111.GUI.ViewModels;
using Xunit;

namespace Scanner111.Tests.GUI.ViewModels;

public class FcxResultViewModelTests
{
    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = true },
                new() { FilePath = "file2.dll", IsValid = false }
            },
            HashValidations = new List<HashValidation>
            {
                new() { FilePath = "hash1.txt", IsValid = true },
                new() { FilePath = "hash2.txt", IsValid = true },
                new() { FilePath = "hash3.txt", IsValid = false }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal(fcxResult, viewModel.FcxResult);
        Assert.Equal(2, viewModel.TotalFileChecks);
        Assert.Equal(1, viewModel.PassedChecks);
        Assert.Equal(1, viewModel.FailedChecks);
        Assert.Equal(3, viewModel.TotalHashValidations);
        Assert.Equal(2, viewModel.PassedValidations);
        Assert.Equal(1, viewModel.FailedValidations);
        Assert.Equal("Good", viewModel.OverallStatus);
        Assert.True(viewModel.HasIssues);
    }

    [Fact]
    public void Constructor_HandlesNullCollections()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Medium,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Warning,
            FileChecks = null,
            HashValidations = null
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal(0, viewModel.TotalFileChecks);
        Assert.Equal(0, viewModel.PassedChecks);
        Assert.Equal(0, viewModel.FailedChecks);
        Assert.Equal(0, viewModel.TotalHashValidations);
        Assert.Equal(0, viewModel.PassedValidations);
        Assert.Equal(0, viewModel.FailedValidations);
        Assert.Equal("Warning", viewModel.OverallStatus);
        Assert.False(viewModel.HasIssues);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(10, 10, 5, 5, false)]
    [InlineData(10, 9, 5, 5, true)]
    [InlineData(10, 10, 5, 4, true)]
    [InlineData(10, 8, 5, 3, true)]
    public void HasIssues_CalculatesCorrectly(int totalFiles, int passedFiles, int totalHashes, int passedHashes, bool expectedHasIssues)
    {
        // Arrange
        var fileChecks = new List<FileIntegrityCheck>();
        for (int i = 0; i < totalFiles; i++)
        {
            fileChecks.Add(new FileIntegrityCheck { FilePath = $"file{i}", IsValid = i < passedFiles });
        }

        var hashValidations = new List<HashValidation>();
        for (int i = 0; i < totalHashes; i++)
        {
            hashValidations.Add(new HashValidation { FilePath = $"hash{i}", IsValid = i < passedHashes });
        }

        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = fileChecks,
            HashValidations = hashValidations
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal(expectedHasIssues, viewModel.HasIssues);
    }

    [Fact]
    public void Summary_FormatsCorrectly()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = true },
                new() { FilePath = "file2.dll", IsValid = true },
                new() { FilePath = "file3.ini", IsValid = false }
            },
            HashValidations = new List<HashValidation>
            {
                new() { FilePath = "hash1.txt", IsValid = true },
                new() { FilePath = "hash2.txt", IsValid = false }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal("2/3 file checks passed, 1/2 hash validations passed", viewModel.Summary);
    }

    [Theory]
    [InlineData(true, "⚠️")]
    [InlineData(false, "✅")]
    public void StatusIcon_ReturnsCorrectIcon(bool hasIssues, string expectedIcon)
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = !hasIssues }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal(expectedIcon, viewModel.StatusIcon);
    }

    [Theory]
    [InlineData(true, "#FF6B6B")]
    [InlineData(false, "#51CF66")]
    public void StatusColor_ReturnsCorrectColor(bool hasIssues, string expectedColor)
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = !hasIssues }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal(expectedColor, viewModel.StatusColor);
    }

    [Theory]
    [InlineData(GameIntegrityStatus.Good, "Good")]
    [InlineData(GameIntegrityStatus.Warning, "Warning")]
    [InlineData(GameIntegrityStatus.Critical, "Critical")]
    [InlineData(GameIntegrityStatus.Invalid, "Invalid")]
    public void OverallStatus_ReturnsCorrectStatus(GameIntegrityStatus status, string expectedStatus)
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = status
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal(expectedStatus, viewModel.OverallStatus);
    }

    [Fact]
    public void AllFileChecksPass_HasIssuesIsFalse()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = true },
                new() { FilePath = "file2.dll", IsValid = true },
                new() { FilePath = "file3.ini", IsValid = true }
            },
            HashValidations = new List<HashValidation>
            {
                new() { FilePath = "hash1.txt", IsValid = true },
                new() { FilePath = "hash2.txt", IsValid = true }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.False(viewModel.HasIssues);
        Assert.Equal("✅", viewModel.StatusIcon);
        Assert.Equal("#51CF66", viewModel.StatusColor);
    }

    [Fact]
    public void EmptyChecks_SummaryFormatsCorrectly()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            Severity = AnalysisSeverity.Low,
            Message = "Test FCX result",
            Details = new List<string>(),
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>(),
            HashValidations = new List<HashValidation>()
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        Assert.Equal("0/0 file checks passed, 0/0 hash validations passed", viewModel.Summary);
        Assert.False(viewModel.HasIssues);
    }
}