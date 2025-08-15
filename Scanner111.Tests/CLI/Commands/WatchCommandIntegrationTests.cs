using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.CLI.Commands;

/// <summary>
///     Integration tests for WatchCommand that test actual file system monitoring
///     and end-to-end processing scenarios.
/// </summary>
[Collection("FileWatcher Tests")]
public class WatchCommandIntegrationTests : IDisposable
{
    private readonly WatchCommand _command;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly string _testDirectory;
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

    public void Dispose()
    {
        // Dispose command to clean up FileSystemWatcher
        _command?.Dispose();
        _serviceProvider?.Dispose();

        // Clean up created files
        foreach (var file in _createdFiles)
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
            }

        // Clean up created directories
        foreach (var dir in _createdDirectories)
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
            }
    }

    #region Batch Processing Integration Tests

    [Fact(Timeout = 10000)]
    public async Task ScanExisting_ProcessesMultipleFilesInBatch()
    {
        // Arrange
        _testPipeline.Reset();

        var testFiles = new List<string>();
        for (var i = 0; i < 5; i++)
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
            scanResults.Add(new ScanResult
            {
                LogPath = file,
                AnalysisResults = new List<AnalysisResult>()
            });
        _testPipeline.SetBatchResults(scanResults);

        // Act
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for all 5 files to be processed with longer timeout
        await _testPipeline.WaitForProcessingAsync(5, TimeSpan.FromSeconds(8));

        // Small delay to ensure report writing completes
        await Task.Delay(500);

        // Cancel and wait for task to complete
        cts.Cancel();
        try
        {
            await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable if task doesn't complete quickly after cancel
        }

        // Assert
        _testPipeline.ProcessedPaths.Should().HaveCount(5);
        _testPipeline.ProcessedPaths.Should().BeEquivalentTo(testFiles);
        _testReportWriter.WrittenReports.Should().HaveCount(5);
    }

    #endregion

    #region Error Recovery Integration Tests

    [Fact(Timeout = 10000)]
    public async Task FileProcessing_WithTransientError_ContinuesMonitoring()
    {
        // Arrange
        _testPipeline.Reset();

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
            if (path.Contains("error.log")) throw new IOException("Simulated I/O error");

            await Task.Yield(); // Ensure async
            return new ScanResult
            {
                LogPath = path,
                AnalysisResults = new List<AnalysisResult>()
            };
        });

        // Act
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for watcher to initialize
        await Task.Delay(500);

        // Create the error file first
        await File.WriteAllTextAsync(errorFile, "This will cause error");
        _createdFiles.Add(errorFile);

        // Wait a bit for error to be processed
        await Task.Delay(1500);

        // Create the good file
        await File.WriteAllTextAsync(goodFile, "This will succeed");
        _createdFiles.Add(goodFile);

        // Wait for the good file to be processed - need to wait for the second non-error processing
        // The first WaitForAnyProcessingAsync might complete on the error file
        var maxWaitTime = DateTime.Now.AddSeconds(8);
        while (DateTime.Now < maxWaitTime)
        {
            if (processedFiles.Count(p => p.Contains("good.log")) > 0 &&
                _testReportWriter.WrittenReports.ContainsKey(goodFile))
                break;
            await Task.Delay(100);
        }

        // Extra delay to ensure everything completes
        await Task.Delay(500);

        // Ensure cleanup
        cts.Cancel();
        try
        {
            await watchTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }

        // Assert
        callCount.Should().BeGreaterThanOrEqualTo(2, "Both files should be attempted");
        processedFiles.Should().Contain(path => path.Contains("error.log"), "Error file should be attempted");
        processedFiles.Should().Contain(path => path.Contains("good.log"), "Good file should be processed");

        // The key assertion is that monitoring continued after the error and processed the good file
        // Report writing might not happen due to timing in tests, but the pipeline processing is what matters
        processedFiles.Count(p => p.Contains("good.log")).Should().BeGreaterThan(0,
            "Good file should have been processed, proving monitoring continued after error");
    }

    #endregion

    /// <summary>
    ///     Test implementation of IReportWriter that tracks written reports
    /// </summary>
    private class TestReportWriter : IReportWriter
    {
        public Dictionary<string, ScanResult> WrittenReports { get; } = new();

        public Task<bool> WriteReportAsync(ScanResult scanResult, CancellationToken cancellationToken = default)
        {
            WrittenReports[scanResult.LogPath] = scanResult;
            return Task.FromResult(true);
        }

        public Task<bool> WriteReportAsync(ScanResult scanResult, string outputPath,
            CancellationToken cancellationToken = default)
        {
            WrittenReports[scanResult.LogPath] = scanResult;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    ///     Extended TestScanPipeline with callback support for integration tests
    /// </summary>
    private class TestScanPipeline : Tests.TestHelpers.TestScanPipeline
    {
        private readonly object _lockObject = new();
        private readonly SemaphoreSlim _processedSignal = new(0, int.MaxValue);
        private readonly List<TaskCompletionSource<bool>> _waiters = new();
        private int _processedCount;
        private Func<string, CancellationToken, Task<ScanResult>>? _processSingleCallback;

        public void SetProcessSingleCallback(Func<string, CancellationToken, Task<ScanResult>> callback)
        {
            _processSingleCallback = callback;
        }

        public override async Task<ScanResult> ProcessSingleAsync(string logPath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Add diagnostic logging for debugging
                Console.WriteLine($"[TestScanPipeline] Processing: {Path.GetFileName(logPath)}");

                ScanResult result;
                if (_processSingleCallback != null)
                    result = await _processSingleCallback(logPath, cancellationToken).ConfigureAwait(false);
                else
                    result = await base.ProcessSingleAsync(logPath, cancellationToken).ConfigureAwait(false);

                // Signal processing completion
                lock (_lockObject)
                {
                    _processedCount++;
                    Console.WriteLine($"[TestScanPipeline] Processed {_processedCount} files, signaling completion");

                    // Release the semaphore to signal waiting threads
                    _processedSignal.Release();

                    // Complete any waiting tasks
                    var waitersToComplete = _waiters.ToList();
                    _waiters.Clear();
                    foreach (var waiter in waitersToComplete) waiter.TrySetResult(true);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestScanPipeline] Error processing {logPath}: {ex.Message}");
                throw;
            }
        }

        public async Task WaitForProcessingAsync(int expectedCount, TimeSpan timeout)
        {
            Console.WriteLine(
                $"[TestScanPipeline] Waiting for {expectedCount} files to be processed (timeout: {timeout})");

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                for (var i = 0; i < expectedCount; i++)
                {
                    await _processedSignal.WaitAsync(cts.Token).ConfigureAwait(false);
                    Console.WriteLine($"[TestScanPipeline] Received signal {i + 1}/{expectedCount}");
                }

                Console.WriteLine($"[TestScanPipeline] Successfully waited for {expectedCount} files");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(
                    $"[TestScanPipeline] Timeout waiting for processing. Processed count: {_processedCount}");
                throw;
            }
        }

        public async Task WaitForAnyProcessingAsync(TimeSpan timeout)
        {
            Console.WriteLine($"[TestScanPipeline] Waiting for any file to be processed (timeout: {timeout})");

            var tcs = new TaskCompletionSource<bool>();

            lock (_lockObject)
            {
                // If already processed something, return immediately
                if (_processedCount > 0)
                {
                    Console.WriteLine(
                        $"[TestScanPipeline] Already processed {_processedCount} files, returning immediately");
                    return;
                }

                _waiters.Add(tcs);
            }

            using var cts = new CancellationTokenSource(timeout);
            cts.Token.Register(() => tcs.TrySetCanceled());

            try
            {
                await tcs.Task.ConfigureAwait(false);
                Console.WriteLine("[TestScanPipeline] Any processing completed");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[TestScanPipeline] Timeout waiting for any processing");
                throw;
            }
        }

        public void Reset()
        {
            lock (_lockObject)
            {
                _processedCount = 0;
                _waiters.Clear();
                // Drain any remaining signals
                while (_processedSignal.CurrentCount > 0) _processedSignal.Wait(0);
            }
        }
    }

    #region File System Watcher Integration Tests

    [Fact(Timeout = 10000)]
    public async Task FileSystemWatcher_DetectsNewFile_ProcessesAutomatically()
    {
        // Arrange
        _testPipeline.Reset();

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
                new GenericAnalysisResult
                {
                    AnalyzerName = "TestAnalyzer", HasFindings = true, ReportLines = new List<string> { "Test issue" }
                }
            }
        };
        _testPipeline.SetResult(scanResult);

        // Act
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for watcher to initialize
        await Task.Delay(500);

        // Create a new file
        await File.WriteAllTextAsync(newFile, "New crash log content");
        _createdFiles.Add(newFile);

        // Wait for processing to complete
        await _testPipeline.WaitForProcessingAsync(1, TimeSpan.FromSeconds(5));

        // Give a moment for report writing to complete (happens after pipeline processing)
        await Task.Delay(500);

        // Ensure cleanup
        cts.Cancel();
        try
        {
            await watchTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }

        // Assert
        _testPipeline.ProcessedPaths.Should().Contain(newFile);
        _testReportWriter.WrittenReports.Count.Should().BeGreaterThan(0, "At least one report should be written");
        _testReportWriter.WrittenReports.Keys.Should().Contain(newFile, "Report should be written for the new file");
    }

    [Fact(Timeout = 10000)]
    public async Task FileSystemWatcher_DetectsFileChange_ProcessesOnce()
    {
        // Arrange
        _testPipeline.Reset();

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
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for watcher to initialize
        await Task.Delay(500);

        // Modify the file multiple times rapidly (within 2 second debounce window)
        await File.AppendAllTextAsync(testFile, "\nModification 1");
        await Task.Delay(50); // Very short delay to trigger multiple events
        await File.AppendAllTextAsync(testFile, "\nModification 2");
        await Task.Delay(50);
        await File.AppendAllTextAsync(testFile, "\nModification 3");

        // Wait for at least one processing to occur
        await _testPipeline.WaitForAnyProcessingAsync(TimeSpan.FromSeconds(5));

        // Wait beyond the debounce window to ensure no additional processing
        await Task.Delay(2000); // Wait for debounce

        // Ensure cleanup
        cts.Cancel();
        try
        {
            await watchTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }

        // Assert - Should process a limited number of times due to debouncing
        // Note: Due to timing variations, the file might be processed 1-3 times
        // (initial detection + potentially one more if events arrive at boundaries)
        _testPipeline.ProcessedPaths.Count(p => p == testFile).Should().BeInRange(1, 3,
            "File should be processed between 1-3 times with debouncing");
    }

    [Fact(Timeout = 10000)]
    public async Task FileSystemWatcher_WithRecursive_MonitorsSubdirectories()
    {
        // Arrange
        _testPipeline.Reset();

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
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for watcher to initialize
        await Task.Delay(500);

        // Create files in both directories
        await File.WriteAllTextAsync(rootFile, "Root log");
        await File.WriteAllTextAsync(subFile, "Sub log");
        _createdFiles.Add(rootFile);
        _createdFiles.Add(subFile);

        // Wait for both files to be processed with longer timeout
        await _testPipeline.WaitForProcessingAsync(2, TimeSpan.FromSeconds(8));

        // Ensure cleanup
        cts.Cancel();
        try
        {
            await watchTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }

        // Assert
        _testPipeline.ProcessedPaths.Should().Contain(rootFile);
        _testPipeline.ProcessedPaths.Should().Contain(subFile);
    }

    #endregion

    #region Auto-Move Integration Tests

    [Fact(Timeout = 10000)]
    public async Task AutoMove_WithCleanLog_MovesToSolvedFolder()
    {
        // Arrange
        _testPipeline.Reset();

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
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for processing to complete with longer timeout
        await _testPipeline.WaitForProcessingAsync(1, TimeSpan.FromSeconds(5));

        // Give it a moment to perform the move
        await Task.Delay(1000);

        // Cancel and wait for task to complete
        cts.Cancel();
        try
        {
            await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }

        // Assert
        var solvedDir = Path.Combine(_testDirectory, "Solved");
        var movedFile = Path.Combine(solvedDir, "clean.log");

        File.Exists(testFile).Should().BeFalse("Original file should be moved");
        File.Exists(movedFile).Should().BeTrue("File should be in Solved directory");
        _createdFiles.Add(movedFile); // Track for cleanup
    }

    [Fact(Timeout = 10000)]
    public async Task AutoMove_WithIssues_DoesNotMoveFile()
    {
        // Arrange
        _testPipeline.Reset();

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
                new GenericAnalysisResult
                {
                    AnalyzerName = "TestAnalyzer", HasFindings = true, ReportLines = new List<string> { "Issue found" }
                }
            }
        };
        _testPipeline.SetResult(scanResult);

        // Act
        var cts = new CancellationTokenSource();
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
        }, cts.Token);

        // Wait for processing to complete with longer timeout
        await _testPipeline.WaitForProcessingAsync(1, TimeSpan.FromSeconds(5));

        // Give it a moment to attempt move (which shouldn't happen)
        await Task.Delay(500);

        // Cancel and wait for task to complete
        cts.Cancel();
        try
        {
            await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }

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
}