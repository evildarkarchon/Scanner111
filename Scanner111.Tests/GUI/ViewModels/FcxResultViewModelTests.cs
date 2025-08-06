using System.Collections.Generic;
using FluentAssertions;
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
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = true },
                new() { FilePath = "file2.dll", IsValid = false }
            },
            HashValidations = new List<HashValidation>
            {
                new() { FilePath = "hash1.txt", ExpectedHash = "abc123", ActualHash = "abc123" },
                new() { FilePath = "hash2.txt", ExpectedHash = "def456", ActualHash = "def456" },
                new() { FilePath = "hash3.txt", ExpectedHash = "ghi789", ActualHash = "different" }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.FcxResult.Should().Be(fcxResult, "because FCX result should be stored");
        viewModel.TotalFileChecks.Should().Be(2, "because there are 2 file checks");
        viewModel.PassedChecks.Should().Be(1, "because 1 file check passed");
        viewModel.FailedChecks.Should().Be(1, "because 1 file check failed");
        viewModel.TotalHashValidations.Should().Be(3, "because there are 3 hash validations");
        viewModel.PassedValidations.Should().Be(2, "because 2 hash validations passed");
        viewModel.FailedValidations.Should().Be(1, "because 1 hash validation failed");
        viewModel.OverallStatus.Should().Be("Good", "because game status is Good");
        viewModel.HasIssues.Should().BeTrue("because there are failed checks");
    }

    [Fact]
    public void Constructor_HandlesNullCollections()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            GameStatus = GameIntegrityStatus.Warning,
            FileChecks = null,
            HashValidations = null
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.TotalFileChecks.Should().Be(0, "because file checks collection is null");
        viewModel.PassedChecks.Should().Be(0, "because there are no checks to pass");
        viewModel.FailedChecks.Should().Be(0, "because there are no checks to fail");
        viewModel.TotalHashValidations.Should().Be(0, "because hash validations collection is null");
        viewModel.PassedValidations.Should().Be(0, "because there are no validations to pass");
        viewModel.FailedValidations.Should().Be(0, "because there are no validations to fail");
        viewModel.OverallStatus.Should().Be("Warning", "because game status is Warning");
        viewModel.HasIssues.Should().BeFalse("because there are no checks to have issues");
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
            var expectedHash = "expected" + i;
            var actualHash = i < passedHashes ? expectedHash : "different" + i;
            hashValidations.Add(new HashValidation { FilePath = $"hash{i}", ExpectedHash = expectedHash, ActualHash = actualHash });
        }

        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = fileChecks,
            HashValidations = hashValidations
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.HasIssues.Should().Be(expectedHasIssues, "because issues calculation should match expected result");
    }

    [Fact]
    public void Summary_FormatsCorrectly()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = true },
                new() { FilePath = "file2.dll", IsValid = true },
                new() { FilePath = "file3.ini", IsValid = false }
            },
            HashValidations = new List<HashValidation>
            {
                new() { FilePath = "hash1.txt", ExpectedHash = "abc123", ActualHash = "abc123" },
                new() { FilePath = "hash2.txt", ExpectedHash = "def456", ActualHash = "different" }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.Summary.Should().Be("2/3 file checks passed, 1/2 hash validations passed", "because summary should show passed/total counts");
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
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = !hasIssues }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.StatusIcon.Should().Be(expectedIcon, "because status icon should reflect issue status");
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
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = !hasIssues }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.StatusColor.Should().Be(expectedColor, "because status color should reflect issue status");
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
            GameStatus = status
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.OverallStatus.Should().Be(expectedStatus, "because overall status should match game integrity status");
    }

    [Fact]
    public void AllFileChecksPass_HasIssuesIsFalse()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>
            {
                new() { FilePath = "file1.exe", IsValid = true },
                new() { FilePath = "file2.dll", IsValid = true },
                new() { FilePath = "file3.ini", IsValid = true }
            },
            HashValidations = new List<HashValidation>
            {
                new() { FilePath = "hash1.txt", ExpectedHash = "abc123", ActualHash = "abc123" },
                new() { FilePath = "hash2.txt", ExpectedHash = "def456", ActualHash = "def456" }
            }
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.HasIssues.Should().BeFalse("because all checks passed");
        viewModel.StatusIcon.Should().Be("✅", "because no issues should show success icon");
        viewModel.StatusColor.Should().Be("#51CF66", "because no issues should show green color");
    }

    [Fact]
    public void EmptyChecks_SummaryFormatsCorrectly()
    {
        // Arrange
        var fcxResult = new FcxScanResult
        {
            AnalyzerName = "FCX Analyzer",
            GameStatus = GameIntegrityStatus.Good,
            FileChecks = new List<FileIntegrityCheck>(),
            HashValidations = new List<HashValidation>()
        };

        // Act
        var viewModel = new FcxResultViewModel(fcxResult);

        // Assert
        viewModel.Summary.Should().Be("0/0 file checks passed, 0/0 hash validations passed", "because summary should handle empty collections");
        viewModel.HasIssues.Should().BeFalse("because empty checks mean no issues");
    }
}