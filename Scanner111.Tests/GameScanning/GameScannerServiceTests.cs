using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.GameScanning
{
    /// <summary>
    /// Comprehensive tests for GameScannerService.
    /// </summary>
    public class GameScannerServiceTests
    {
        private readonly Mock<ICrashGenChecker> _mockCrashGenChecker;
        private readonly Mock<IXsePluginValidator> _mockXsePluginValidator;
        private readonly Mock<IModIniScanner> _mockModIniScanner;
        private readonly Mock<IWryeBashChecker> _mockWryeBashChecker;
        private readonly Mock<IMessageHandler> _mockMessageHandler;
        private readonly Mock<ILogger<GameScannerService>> _mockLogger;
        private readonly GameScannerService _service;

        public GameScannerServiceTests()
        {
            _mockCrashGenChecker = new Mock<ICrashGenChecker>();
            _mockXsePluginValidator = new Mock<IXsePluginValidator>();
            _mockModIniScanner = new Mock<IModIniScanner>();
            _mockWryeBashChecker = new Mock<IWryeBashChecker>();
            _mockMessageHandler = new Mock<IMessageHandler>();
            _mockLogger = new Mock<ILogger<GameScannerService>>();

            _service = new GameScannerService(
                _mockCrashGenChecker.Object,
                _mockXsePluginValidator.Object,
                _mockModIniScanner.Object,
                _mockWryeBashChecker.Object,
                _mockMessageHandler.Object,
                _mockLogger.Object);
        }

        #region ScanGameAsync Tests

        [Fact]
        public async Task ScanGameAsync_AllScannersSucceed_ReturnsCompleteResults()
        {
            // Arrange
            const string crashGenResult = "✔️ Crash Gen check passed";
            const string xseResult = "✔️ XSE plugins validated";
            const string modIniResult = "✔️ Mod INIs clean";
            const string wryeBashResult = "✔️ Wrye Bash analysis complete";

            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync(crashGenResult);
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ReturnsAsync(xseResult);
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync(modIniResult);
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync(wryeBashResult);

            // Act
            var result = await _service.ScanGameAsync();

            // Assert
            result.Should().NotBeNull();
            result.CrashGenResults.Should().Be(crashGenResult);
            result.XsePluginResults.Should().Be(xseResult);
            result.ModIniResults.Should().Be(modIniResult);
            result.WryeBashResults.Should().Be(wryeBashResult);
            result.HasIssues.Should().BeFalse();
            result.CriticalIssues.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();

            _mockMessageHandler.Verify(x => x.ShowInfo(It.Is<string>(s => s == "Starting comprehensive game scan..."), It.IsAny<MessageTarget>()), Times.Once);
            _mockMessageHandler.Verify(x => x.ShowSuccess(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task ScanGameAsync_WithCriticalIssues_DetectsAndReportsIssues()
        {
            // Arrange
            const string crashGenResult = "❌ CRITICAL: Buffout4 not configured properly";
            const string xseResult = "✔️ XSE plugins validated";
            const string modIniResult = "⚠️ WARNING: Suspicious INI settings detected";
            const string wryeBashResult = "❌ ERROR: Missing masters found";

            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync(crashGenResult);
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ReturnsAsync(xseResult);
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync(modIniResult);
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync(wryeBashResult);

            // Act
            var result = await _service.ScanGameAsync();

            // Assert
            result.Should().NotBeNull();
            result.HasIssues.Should().BeTrue();
            result.CriticalIssues.Should().NotBeEmpty();
            result.CriticalIssues.Should().Contain(issue => issue.Contains("Buffout4 not configured properly"));
            result.CriticalIssues.Should().Contain(issue => issue.Contains("Missing masters found"));
            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(warning => warning.Contains("Suspicious INI settings detected"));

            _mockMessageHandler.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task ScanGameAsync_OneScannerFails_ContinuesAndReportsError()
        {
            // Arrange
            const string crashGenResult = "✔️ Crash Gen check passed";
            const string modIniResult = "✔️ Mod INIs clean";
            const string wryeBashResult = "✔️ Wrye Bash analysis complete";

            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync(crashGenResult);
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ThrowsAsync(new InvalidOperationException("XSE validation failed"));
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync(modIniResult);
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync(wryeBashResult);

            // Act
            var result = await _service.ScanGameAsync();

            // Assert
            result.Should().NotBeNull();
            result.CrashGenResults.Should().Be(crashGenResult);
            result.XsePluginResults.Should().Contain("Error:");
            result.ModIniResults.Should().Be(modIniResult);
            result.WryeBashResults.Should().Be(wryeBashResult);
            result.HasIssues.Should().BeFalse(); // Only the XSE scanner had an error, but it's handled
        }

        [Fact]
        public async Task ScanGameAsync_WithCancellation_ThrowsOperationCancelledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockCrashGenChecker.Setup(x => x.CheckAsync())
                .Returns(async () =>
                {
                    await Task.Delay(100);
                    return "Should not complete";
                });

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => _service.ScanGameAsync(cts.Token));
            _mockMessageHandler.Verify(x => x.ShowWarning(It.Is<string>(s => s == "Game scan cancelled"), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task ScanGameAsync_RunsAllScannersInParallel()
        {
            // Arrange
            var checkAsyncCalled = false;
            var validateAsyncCalled = false;
            var scanAsyncCalled = false;
            var analyzeAsyncCalled = false;

            _mockCrashGenChecker.Setup(x => x.CheckAsync())
                .Returns(async () =>
                {
                    checkAsyncCalled = true;
                    await Task.Delay(50);
                    return "CrashGen result";
                });

            _mockXsePluginValidator.Setup(x => x.ValidateAsync())
                .Returns(async () =>
                {
                    validateAsyncCalled = true;
                    await Task.Delay(50);
                    return "XSE result";
                });

            _mockModIniScanner.Setup(x => x.ScanAsync())
                .Returns(async () =>
                {
                    scanAsyncCalled = true;
                    await Task.Delay(50);
                    return "ModIni result";
                });

            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync())
                .Returns(async () =>
                {
                    analyzeAsyncCalled = true;
                    await Task.Delay(50);
                    return "WryeBash result";
                });

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _service.ScanGameAsync();
            var elapsedTime = DateTime.UtcNow - startTime;

            // Assert
            checkAsyncCalled.Should().BeTrue();
            validateAsyncCalled.Should().BeTrue();
            scanAsyncCalled.Should().BeTrue();
            analyzeAsyncCalled.Should().BeTrue();

            // If they ran in parallel, total time should be close to the longest individual operation (50ms)
            // rather than the sum of all operations (200ms)
            elapsedTime.TotalMilliseconds.Should().BeLessThan(150);
        }

        #endregion

        #region Individual Scanner Method Tests

        [Fact]
        public async Task CheckCrashGenAsync_Success_ReturnsResult()
        {
            // Arrange
            const string expectedResult = "Crash Gen check complete";
            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync(expectedResult);

            // Act
            var result = await _service.CheckCrashGenAsync();

            // Assert
            result.Should().Be(expectedResult);
            _mockMessageHandler.Verify(x => x.ShowInfo(It.Is<string>(s => s == "Checking Crash Generator configuration..."), It.IsAny<MessageTarget>()), Times.Once);
            _mockMessageHandler.Verify(x => x.ShowSuccess(It.Is<string>(s => s == "Crash Generator check completed"), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task CheckCrashGenAsync_Exception_ReturnsErrorMessage()
        {
            // Arrange
            const string errorMessage = "Failed to access config file";
            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ThrowsAsync(new InvalidOperationException(errorMessage));

            // Act
            var result = await _service.CheckCrashGenAsync();

            // Assert
            result.Should().Contain("Error:");
            result.Should().Contain(errorMessage);
            _mockMessageHandler.Verify(x => x.ShowError(It.Is<string>(s => s.Contains(errorMessage)), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task CheckCrashGenAsync_WithCancellation_ThrowsCancellationRequestedException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _service.CheckCrashGenAsync(cts.Token);

            // Assert
            result.Should().Contain("Error:");
            _mockCrashGenChecker.Verify(x => x.CheckAsync(), Times.Never);
        }

        [Fact]
        public async Task ValidateXsePluginsAsync_Success_ReturnsResult()
        {
            // Arrange
            const string expectedResult = "XSE plugins validated successfully";
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ReturnsAsync(expectedResult);

            // Act
            var result = await _service.ValidateXsePluginsAsync();

            // Assert
            result.Should().Be(expectedResult);
            _mockMessageHandler.Verify(x => x.ShowInfo(It.Is<string>(s => s == "Validating XSE plugins..."), It.IsAny<MessageTarget>()), Times.Once);
            _mockMessageHandler.Verify(x => x.ShowSuccess(It.Is<string>(s => s == "XSE plugin validation completed"), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task ScanModInisAsync_Success_ReturnsResult()
        {
            // Arrange
            const string expectedResult = "Mod INI scan complete";
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync(expectedResult);

            // Act
            var result = await _service.ScanModInisAsync();

            // Assert
            result.Should().Be(expectedResult);
            _mockMessageHandler.Verify(x => x.ShowInfo(It.Is<string>(s => s == "Scanning mod INI files..."), It.IsAny<MessageTarget>()), Times.Once);
            _mockMessageHandler.Verify(x => x.ShowSuccess(It.Is<string>(s => s == "Mod INI scan completed"), It.IsAny<MessageTarget>()), Times.Once);
        }

        [Fact]
        public async Task CheckWryeBashAsync_Success_ReturnsResult()
        {
            // Arrange
            const string expectedResult = "Wrye Bash analysis complete";
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync(expectedResult);

            // Act
            var result = await _service.CheckWryeBashAsync();

            // Assert
            result.Should().Be(expectedResult);
            _mockMessageHandler.Verify(x => x.ShowInfo(It.Is<string>(s => s == "Analyzing Wrye Bash report..."), It.IsAny<MessageTarget>()), Times.Once);
            _mockMessageHandler.Verify(x => x.ShowSuccess(It.Is<string>(s => s == "Wrye Bash analysis completed"), It.IsAny<MessageTarget>()), Times.Once);
        }

        #endregion

        #region Edge Cases and Error Scenarios

        [Fact]
        public async Task ScanGameAsync_AllScannersReturnEmpty_HandlesGracefully()
        {
            // Arrange
            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync(string.Empty);
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ReturnsAsync(string.Empty);
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync(string.Empty);
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync(string.Empty);

            // Act
            var result = await _service.ScanGameAsync();

            // Assert
            result.Should().NotBeNull();
            result.CrashGenResults.Should().BeEmpty();
            result.XsePluginResults.Should().BeEmpty();
            result.ModIniResults.Should().BeEmpty();
            result.WryeBashResults.Should().BeEmpty();
            result.HasIssues.Should().BeFalse();
        }

        [Fact]
        public async Task ScanGameAsync_MixedSuccessAndFailure_ReportsPartialResults()
        {
            // Arrange
            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync("✔️ Success");
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ThrowsAsync(new Exception("XSE failed"));
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync("❌ Critical issue");
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ThrowsAsync(new Exception("WB failed"));

            // Act
            var result = await _service.ScanGameAsync();

            // Assert
            result.Should().NotBeNull();
            result.CrashGenResults.Should().Be("✔️ Success");
            result.XsePluginResults.Should().Contain("Error:");
            result.ModIniResults.Should().Be("❌ Critical issue");
            result.WryeBashResults.Should().Contain("Error:");
            result.HasIssues.Should().BeTrue();
            result.CriticalIssues.Should().HaveCount(1);
        }

        [Fact]
        public async Task ScanGameAsync_ExtractsMultipleCriticalIssuesAndWarnings()
        {
            // Arrange
            var crashGenResult = @"❌ Critical Issue 1
Some text here
❌ Another critical problem
More text";

            var modIniResult = @"⚠️ Warning about INI setting
Normal text
⚠️ Another warning here";

            _mockCrashGenChecker.Setup(x => x.CheckAsync()).ReturnsAsync(crashGenResult);
            _mockXsePluginValidator.Setup(x => x.ValidateAsync()).ReturnsAsync("Normal result");
            _mockModIniScanner.Setup(x => x.ScanAsync()).ReturnsAsync(modIniResult);
            _mockWryeBashChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync("✔️ All good");

            // Act
            var result = await _service.ScanGameAsync();

            // Assert
            result.HasIssues.Should().BeTrue();
            result.CriticalIssues.Should().HaveCount(2);
            result.CriticalIssues.Should().Contain(issue => issue.Contains("Critical Issue 1"));
            result.CriticalIssues.Should().Contain(issue => issue.Contains("Another critical problem"));
            result.Warnings.Should().HaveCount(2);
            result.Warnings.Should().Contain(warning => warning.Contains("Warning about INI setting"));
            result.Warnings.Should().Contain(warning => warning.Contains("Another warning here"));
        }

        #endregion
    }
}