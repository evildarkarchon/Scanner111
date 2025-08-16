using System;
using System.Collections.Generic;
using FluentAssertions;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.GameScanning
{
    /// <summary>
    /// Tests for GameScanResult model.
    /// </summary>
    public class GameScanResultTests
    {
        [Fact]
        public void Constructor_InitializesDefaultValues()
        {
            // Act
            var result = new GameScanResult();

            // Assert
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            result.CrashGenResults.Should().BeEmpty();
            result.XsePluginResults.Should().BeEmpty();
            result.ModIniResults.Should().BeEmpty();
            result.WryeBashResults.Should().BeEmpty();
            result.HasIssues.Should().BeFalse();
            result.CriticalIssues.Should().NotBeNull().And.BeEmpty();
            result.Warnings.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void GetFullReport_WithNoResults_ReturnsBasicReport()
        {
            // Arrange
            var result = new GameScanResult();

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().Contain("=== GAME SCAN RESULTS ===");
            report.Should().Contain($"Scan performed at: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
            report.Should().NotContain("--- Crash Generator Check ---");
            report.Should().NotContain("--- XSE Plugin Validation ---");
            report.Should().NotContain("--- Mod INI Scan ---");
            report.Should().NotContain("--- Wrye Bash Check ---");
            report.Should().NotContain("=== CRITICAL ISSUES ===");
            report.Should().NotContain("=== WARNINGS ===");
        }

        [Fact]
        public void GetFullReport_WithAllResults_IncludesAllSections()
        {
            // Arrange
            var result = new GameScanResult
            {
                CrashGenResults = "Crash Gen check passed",
                XsePluginResults = "XSE plugins validated",
                ModIniResults = "Mod INIs clean",
                WryeBashResults = "Wrye Bash analysis complete"
            };

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().Contain("=== GAME SCAN RESULTS ===");
            report.Should().Contain("--- Crash Generator Check ---");
            report.Should().Contain("Crash Gen check passed");
            report.Should().Contain("--- XSE Plugin Validation ---");
            report.Should().Contain("XSE plugins validated");
            report.Should().Contain("--- Mod INI Scan ---");
            report.Should().Contain("Mod INIs clean");
            report.Should().Contain("--- Wrye Bash Check ---");
            report.Should().Contain("Wrye Bash analysis complete");
        }

        [Fact]
        public void GetFullReport_WithCriticalIssues_IncludesCriticalSection()
        {
            // Arrange
            var result = new GameScanResult
            {
                HasIssues = true,
                CriticalIssues = new List<string>
                {
                    "Missing Address Library",
                    "Buffout4 configuration error",
                    "Invalid plugin detected"
                }
            };

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().Contain("=== CRITICAL ISSUES ===");
            report.Should().Contain("❌ Missing Address Library");
            report.Should().Contain("❌ Buffout4 configuration error");
            report.Should().Contain("❌ Invalid plugin detected");
        }

        [Fact]
        public void GetFullReport_WithWarnings_IncludesWarningsSection()
        {
            // Arrange
            var result = new GameScanResult
            {
                HasIssues = true,
                Warnings = new List<string>
                {
                    "Outdated plugin version",
                    "Non-optimal INI setting",
                    "Potential mod conflict"
                }
            };

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().Contain("=== WARNINGS ===");
            report.Should().Contain("⚠️ Outdated plugin version");
            report.Should().Contain("⚠️ Non-optimal INI setting");
            report.Should().Contain("⚠️ Potential mod conflict");
        }

        [Fact]
        public void GetFullReport_CompleteScenario_FormatsCorrectly()
        {
            // Arrange
            var timestamp = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
            var result = new GameScanResult
            {
                Timestamp = timestamp,
                CrashGenResults = "Buffout4 configuration checked\n✔️ All settings correct",
                XsePluginResults = "❌ Missing Address Library\n❌ Outdated F4SE plugin",
                ModIniResults = "⚠️ Suspicious INI setting found",
                WryeBashResults = "Analysis complete\n✔️ No issues found",
                HasIssues = true,
                CriticalIssues = new List<string>
                {
                    "Missing Address Library",
                    "Outdated F4SE plugin"
                },
                Warnings = new List<string>
                {
                    "Suspicious INI setting found"
                }
            };

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().Contain("Scan performed at: 2024-01-15 10:30:45");
            
            // Verify sections appear in correct order
            var crashGenIndex = report.IndexOf("--- Crash Generator Check ---");
            var xseIndex = report.IndexOf("--- XSE Plugin Validation ---");
            var modIniIndex = report.IndexOf("--- Mod INI Scan ---");
            var wryeBashIndex = report.IndexOf("--- Wrye Bash Check ---");
            var criticalIndex = report.IndexOf("=== CRITICAL ISSUES ===");
            var warningsIndex = report.IndexOf("=== WARNINGS ===");

            crashGenIndex.Should().BePositive();
            xseIndex.Should().BeGreaterThan(crashGenIndex);
            modIniIndex.Should().BeGreaterThan(xseIndex);
            wryeBashIndex.Should().BeGreaterThan(modIniIndex);
            criticalIndex.Should().BeGreaterThan(wryeBashIndex);
            warningsIndex.Should().BeGreaterThan(criticalIndex);
        }

        [Fact]
        public void GetFullReport_WithEmptyStrings_SkipsSections()
        {
            // Arrange
            var result = new GameScanResult
            {
                CrashGenResults = "",
                XsePluginResults = "   ", // Whitespace only
                ModIniResults = "\n\t", // Whitespace only
                WryeBashResults = "Valid result"
            };

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().NotContain("--- Crash Generator Check ---");
            report.Should().NotContain("--- XSE Plugin Validation ---");
            report.Should().NotContain("--- Mod INI Scan ---");
            report.Should().Contain("--- Wrye Bash Check ---");
            report.Should().Contain("Valid result");
        }

        [Fact]
        public void GetFullReport_WithNullResults_HandlesGracefully()
        {
            // Arrange
            var result = new GameScanResult
            {
                CrashGenResults = null!,
                XsePluginResults = null!,
                ModIniResults = null!,
                WryeBashResults = null!
            };

            // Act
            var report = result.GetFullReport();

            // Assert
            report.Should().NotBeNull();
            report.Should().Contain("=== GAME SCAN RESULTS ===");
            report.Should().NotContain("--- Crash Generator Check ---");
            report.Should().NotContain("--- XSE Plugin Validation ---");
            report.Should().NotContain("--- Mod INI Scan ---");
            report.Should().NotContain("--- Wrye Bash Check ---");
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, false, false)]
        public void HasIssues_Property_WorksCorrectly(bool hasIssues, bool addCritical, bool addWarnings)
        {
            // Arrange
            var result = new GameScanResult
            {
                HasIssues = hasIssues
            };

            if (addCritical)
            {
                result.CriticalIssues.Add("Critical issue");
            }

            if (addWarnings)
            {
                result.Warnings.Add("Warning");
            }

            // Act & Assert
            result.HasIssues.Should().Be(hasIssues);
        }

        [Fact]
        public void Timestamp_CanBeSetAndRetrieved()
        {
            // Arrange
            var customTime = new DateTime(2023, 12, 25, 15, 30, 0, DateTimeKind.Utc);
            var result = new GameScanResult();

            // Act
            result.Timestamp = customTime;

            // Assert
            result.Timestamp.Should().Be(customTime);
            result.GetFullReport().Should().Contain("2023-12-25 15:30:00");
        }
    }
}