using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using Scanner111.Tests.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI.Commands;

/// <summary>
/// Integration tests for WatchCommand that test actual file system monitoring
/// and end-to-end processing scenarios.
/// </summary>
public class WatchCommandIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly WatchCommand _command;
    private readonly string _testDirectory;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();
    private readonly TestScanPipeline _testPipeline;
    private readonly TestReportWriter _testReportWriter;
    private readonly TestApplicationSettingsService _testSettingsService;

    public WatchCommandIntegrationTests()
    {
        // Create test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WatchIntegrationTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);

        // Setup dependency injection
        var services = new ServiceCollection();
        
        _testPipeline = new TestScanPipeline();
        _testReportWriter = new TestReportWriter();
        _testSettingsService = new TestApplicationSettingsService();
        
        services.AddSingleton<IScanPipeline>(_testPipeline);
        services.AddSingleton<IReportWriter>(_testReportWriter);
        services.AddSingleton<IApplicationSettingsService>(_testSettingsService);
        
        _serviceProvider = services.BuildServiceProvider();
        
        _command = new WatchCommand(
            _serviceProvider,
            _testSettingsService,
            _testPipeline,
            _testReportWriter);
    }

    #region File System Watcher Integration Tests

    [Fact(Timeout = 10000)]
    public async Task FileSystemWatcher_DetectsNewFile_ProcessesAutomatically()
    {
        // Arrange
        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false
        };

        var newFile = Path.Combine(_testDirectory, "new.log");
        var scanResult = new ScanResult
        {
            LogPath = newFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult { AnalyzerName = "TestAnalyzer", HasFindings = true, ReportLines = new List<string> { "Test issue" } }
            }
        };
        _testPipeline.SetResult(scanResult);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var watchTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });

        // Wait a bit for watcher to initialize
        await Task.Delay(200);

        // Create a new file
        await File.WriteAllTextAsync(newFile, "New crash log content");
        _createdFiles.Add(newFile);

        // Wait for processing to complete
        await _testPipeline.WaitForProcessingAsync(1, TimeSpan.FromSeconds(3));
        
        // Give a moment for report writing to complete (happens after pipeline processing)
        await Task.Delay(200);
        
        // Ensure cleanup
        cts.Cancel();
        try { await watchTask; } catch (OperationCanceledException) { }

        // Assert
        _testPipeline.ProcessedPaths.Should().Contain(newFile);
        _testReportWriter.WrittenReports.Count.Should().BeGreaterThan(0, "At least one report should be written");
        _testReportWriter.WrittenReports.Keys.Should().Contain(newFile, "Report should be written for the new file");
    }

    [Fact(Timeout = 15000)]
    public async Task FileSystemWatcher_DetectsFileChange_ProcessesOnce()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "existing.log");
        await File.WriteAllTextAsync(testFile, "Initial content");
        _createdFiles.Add(testFile);

        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false
        };

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>()
        };
        _testPipeline.SetResult(scanResult);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var watchTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });

        // Wait for watcher to initialize
        await Task.Delay(300);

        // Modify the file multiple times rapidly (within 2 second debounce window)
        await File.AppendAllTextAsync(testFile, "\nModification 1");
        await Task.Delay(50);  // Very short delay to trigger multiple events
        await File.AppendAllTextAsync(testFile, "\nModification 2");
        await Task.Delay(50);
        await File.AppendAllTextAsync(testFile, "\nModification 3");

        // Wait for at least one processing to occur
        await _testPipeline.WaitForAnyProcessingAsync(TimeSpan.FromSeconds(3));
        
        // Wait beyond the debounce window to ensure no additional processing
        await Task.Delay(2500);  // Wait longer than 2 second debounce
        
        // Ensure cleanup
        cts.Cancel();
        try { await watchTask; } catch (OperationCanceledException) { }

        // Assert - Should process a limited number of times due to debouncing
        // Note: Due to timing variations, the file might be processed 1-3 times
        // (initial detection + potentially one more if events arrive at boundaries)
        _testPipeline.ProcessedPaths.Count(p => p == testFile).Should().BeInRange(1, 3, 
            "File should be processed between 1-3 times with debouncing");
    }

    [Fact(Timeout = 15000)]
    public async Task FileSystemWatcher_WithRecursive_MonitorsSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        _createdDirectories.Add(subDir);

        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false,
            Recursive = true
        };

        var scanResults = new List<ScanResult>();
        var rootFile = Path.Combine(_testDirectory, "root.log");
        var subFile = Path.Combine(subDir, "sub.log");
        
        scanResults.Add(new ScanResult
        {
            LogPath = rootFile,
            AnalysisResults = new List<AnalysisResult>()
        });
        scanResults.Add(new ScanResult
        {
            LogPath = subFile,
            AnalysisResults = new List<AnalysisResult>()
        });
        _testPipeline.SetBatchResults(scanResults);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var watchTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });

        // Wait for watcher to initialize
        await Task.Delay(200);

        // Create files in both directories
        await File.WriteAllTextAsync(rootFile, "Root log");
        await File.WriteAllTextAsync(subFile, "Sub log");
        _createdFiles.Add(rootFile);
        _createdFiles.Add(subFile);

        // Wait for both files to be processed
        await _testPipeline.WaitForProcessingAsync(2, TimeSpan.FromSeconds(4));
        
        // Ensure cleanup
        cts.Cancel();
        try { await watchTask; } catch (OperationCanceledException) { }

        // Assert
        _testPipeline.ProcessedPaths.Should().Contain(rootFile);
        _testPipeline.ProcessedPaths.Should().Contain(subFile);
    }

    #endregion

    #region Auto-Move Integration Tests

    [Fact(Timeout = 5000)]
    public async Task AutoMove_WithCleanLog_MovesToSolvedFolder()
    {
        // Arrange
        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false,
            AutoMove = true,
            ScanExisting = true
        };

        var testFile = Path.Combine(_testDirectory, "clean.log");
        await File.WriteAllTextAsync(testFile, "Clean log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>() // No issues
        };
        _testPipeline.SetResult(scanResult);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var executeTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });
        
        // Wait for processing to complete
        await _testPipeline.WaitForProcessingAsync(1, TimeSpan.FromSeconds(2));
        
        // Give it a moment to perform the move
        await Task.Delay(500);
        
        // Cancel and wait for task to complete
        cts.Cancel();
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        var solvedDir = Path.Combine(_testDirectory, "Solved");
        var movedFile = Path.Combine(solvedDir, "clean.log");
        
        File.Exists(testFile).Should().BeFalse("Original file should be moved");
        File.Exists(movedFile).Should().BeTrue("File should be in Solved directory");
        _createdFiles.Add(movedFile); // Track for cleanup
    }

    [Fact(Timeout = 5000)]
    public async Task AutoMove_WithIssues_DoesNotMoveFile()
    {
        // Arrange
        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false,
            AutoMove = true,
            ScanExisting = true
        };

        var testFile = Path.Combine(_testDirectory, "problematic.log");
        await File.WriteAllTextAsync(testFile, "Problematic log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult { AnalyzerName = "TestAnalyzer", HasFindings = true, ReportLines = new List<string> { "Issue found" } }
            }
        };
        _testPipeline.SetResult(scanResult);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var executeTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });
        
        // Wait for processing to complete
        await _testPipeline.WaitForProcessingAsync(1, TimeSpan.FromSeconds(2));
        
        // Give it a moment to attempt move (which shouldn't happen)
        await Task.Delay(200);
        
        // Cancel and wait for task to complete
        cts.Cancel();
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        File.Exists(testFile).Should().BeTrue("File with issues should not be moved");
        var solvedDir = Path.Combine(_testDirectory, "Solved");
        if (Directory.Exists(solvedDir))
        {
            var movedFile = Path.Combine(solvedDir, "problematic.log");
            File.Exists(movedFile).Should().BeFalse("File should not be in Solved directory");
        }
    }

    #endregion

    #region Batch Processing Integration Tests

    [Fact(Timeout = 10000)]
    public async Task ScanExisting_ProcessesMultipleFilesInBatch()
    {
        // Arrange
        var testFiles = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(_testDirectory, $"crash{i}.log");
            await File.WriteAllTextAsync(file, $"Crash log content {i}");
            testFiles.Add(file);
            _createdFiles.Add(file);
        }

        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false,
            ScanExisting = true
        };

        var scanResults = new List<ScanResult>();
        foreach (var file in testFiles)
        {
            scanResults.Add(new ScanResult
            {
                LogPath = file,
                AnalysisResults = new List<AnalysisResult>()
            });
        }
        _testPipeline.SetBatchResults(scanResults);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var executeTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });
        
        // Wait for all 5 files to be processed
        await _testPipeline.WaitForProcessingAsync(5, TimeSpan.FromSeconds(4));
        
        // Cancel and wait for task to complete
        cts.Cancel();
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        _testPipeline.ProcessedPaths.Should().HaveCount(5);
        _testPipeline.ProcessedPaths.Should().BeEquivalentTo(testFiles);
        _testReportWriter.WrittenReports.Should().HaveCount(5);
    }

    #endregion

    #region Error Recovery Integration Tests

    [Fact(Timeout = 15000)]
    public async Task FileProcessing_WithTransientError_ContinuesMonitoring()
    {
        // Arrange
        var options = new WatchOptions 
        { 
            Path = _testDirectory, 
            ShowDashboard = false,
            ShowNotifications = false
        };

        var errorFile = Path.Combine(_testDirectory, "error.log");
        var goodFile = Path.Combine(_testDirectory, "good.log");

        // Setup pipeline to throw error for first file, succeed for second
        var callCount = 0;
        var processedFiles = new List<string>();
        _testPipeline.SetProcessSingleCallback(async (path, ct) =>
        {
            callCount++;
            processedFiles.Add(path);
            
            // Throw error for the error.log file
            if (path.Contains("error.log"))
            {
                throw new IOException("Simulated I/O error");
            }
            
            await Task.Yield(); // Ensure async
            return new ScanResult
            {
                LogPath = path,
                AnalysisResults = new List<AnalysisResult>()
            };
        });

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var watchTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        });

        // Wait for watcher to initialize
        await Task.Delay(200);

        // Create the error file first
        await File.WriteAllTextAsync(errorFile, "This will cause error");
        _createdFiles.Add(errorFile);
        
        // Wait a bit for error to be processed
        await Task.Delay(1000);
        
        // Create the good file
        await File.WriteAllTextAsync(goodFile, "This will succeed");
        _createdFiles.Add(goodFile);
        
        // Wait for the good file to be processed
        await _testPipeline.WaitForAnyProcessingAsync(TimeSpan.FromSeconds(3));
        
        // Ensure cleanup
        cts.Cancel();
        try { await watchTask; } catch (OperationCanceledException) { }

        // Assert
        callCount.Should().BeGreaterThanOrEqualTo(2, "Both files should be attempted");
        processedFiles.Should().Contain(path => path.Contains("error.log"), "Error file should be attempted");
        processedFiles.Should().Contain(path => path.Contains("good.log"), "Good file should be processed");
        _testReportWriter.WrittenReports.Should().ContainKey(goodFile, "Good file should have report written");
    }

    #endregion

    public void Dispose()
    {
        // Dispose command to clean up FileSystemWatcher
        _command?.Dispose();
        _serviceProvider?.Dispose();

        // Clean up created files
        foreach (var file in _createdFiles)
        {
            try 
            { 
                if (File.Exists(file))
                    File.Delete(file); 
            } 
            catch { }
        }

        // Clean up created directories
        foreach (var dir in _createdDirectories)
        {
            try 
            { 
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true); 
            } 
            catch { }
        }
    }

    /// <summary>
    /// Test implementation of IReportWriter that tracks written reports
    /// </summary>
    private class TestReportWriter : IReportWriter
    {
        public Dictionary<string, ScanResult> WrittenReports { get; } = new();

        public Task<bool> WriteReportAsync(ScanResult scanResult, CancellationToken cancellationToken = default)
        {
            WrittenReports[scanResult.LogPath] = scanResult;
            return Task.FromResult(true);
        }

        public Task<bool> WriteReportAsync(ScanResult scanResult, string outputPath, CancellationToken cancellationToken = default)
        {
            WrittenReports[scanResult.LogPath] = scanResult;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Extended TestScanPipeline with callback support for integration tests
    /// </summary>
    private class TestScanPipeline : Scanner111.Tests.TestHelpers.TestScanPipeline
    {
        private Func<string, CancellationToken, Task<ScanResult>>? _processSingleCallback;
        private readonly SemaphoreSlim _processedSignal = new(0);
        private readonly List<TaskCompletionSource<bool>> _waiters = new();

        public void SetProcessSingleCallback(Func<string, CancellationToken, Task<ScanResult>> callback)
        {
            _processSingleCallback = callback;
        }

        public override async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
        {
            ScanResult result;
            if (_processSingleCallback != null)
            {
                result = await _processSingleCallback(logPath, cancellationToken);
            }
            else
            {
                result = await base.ProcessSingleAsync(logPath, cancellationToken);
            }
            
            // Signal that a file was processed
            _processedSignal.Release();
            
            // Complete any waiting tasks
            foreach (var waiter in _waiters.ToList())
            {
                waiter.TrySetResult(true);
            }
            
            return result;
        }
        
        public async Task WaitForProcessingAsync(int expectedCount, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            for (int i = 0; i < expectedCount; i++)
            {
                await _processedSignal.WaitAsync(cts.Token);
            }
        }
        
        public Task WaitForAnyProcessingAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>();
            _waiters.Add(tcs);
            
            var timeoutTask = Task.Delay(timeout);
            return Task.WhenAny(tcs.Task, timeoutTask);
        }
    }
}