using System.Collections.Concurrent;

namespace Scanner111.Test.Infrastructure.Fixtures;

/// <summary>
/// Provides shared temporary directory management for test collections.
/// Thread-safe and automatically cleans up after test execution.
/// </summary>
public sealed class TempDirectoryFixture : IAsyncLifetime, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _testDirectories;
    private readonly SemaphoreSlim _directoryCreationLock;
    private readonly string _rootTempPath;
    private bool _disposed;

    public TempDirectoryFixture()
    {
        _testDirectories = new ConcurrentDictionary<string, string>();
        _directoryCreationLock = new SemaphoreSlim(1, 1);
        
        // Create a unique root temp directory for this test run
        _rootTempPath = Path.Combine(
            Path.GetTempPath(), 
            $"Scanner111_Tests_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Gets the root temporary directory for all tests in this collection.
    /// </summary>
    public string RootPath => _rootTempPath;

    /// <summary>
    /// Gets or creates a unique temporary directory for a specific test.
    /// Thread-safe - can be called concurrently from parallel tests.
    /// </summary>
    /// <param name="testName">The name of the test requesting the directory.</param>
    /// <returns>Path to the test-specific temporary directory.</returns>
    public async Task<string> GetTestDirectoryAsync(string testName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TempDirectoryFixture));

        return await Task.Run(() =>
        {
            return _testDirectories.GetOrAdd(testName, name =>
            {
                var sanitizedName = SanitizeTestName(name);
                var testDir = Path.Combine(_rootTempPath, sanitizedName);
                Directory.CreateDirectory(testDir);
                return testDir;
            });
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets or creates a unique temporary directory synchronously.
    /// </summary>
    public string GetTestDirectory(string testName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TempDirectoryFixture));

        return _testDirectories.GetOrAdd(testName, name =>
        {
            var sanitizedName = SanitizeTestName(name);
            var testDir = Path.Combine(_rootTempPath, sanitizedName);
            Directory.CreateDirectory(testDir);
            return testDir;
        });
    }

    /// <summary>
    /// Creates a temporary file in the specified test directory.
    /// </summary>
    public async Task<string> CreateTempFileAsync(string testName, string fileName, string content)
    {
        var testDir = await GetTestDirectoryAsync(testName).ConfigureAwait(false);
        var filePath = Path.Combine(testDir, fileName);
        await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
        return filePath;
    }

    /// <summary>
    /// Copies a file to the test's temporary directory.
    /// </summary>
    public async Task<string> CopyToTestDirectoryAsync(string testName, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath}");

        var testDir = await GetTestDirectoryAsync(testName).ConfigureAwait(false);
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(testDir, fileName);
        
        await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true)).ConfigureAwait(false);
        return destPath;
    }

    /// <summary>
    /// Cleans up a specific test's directory.
    /// </summary>
    public async Task CleanupTestDirectoryAsync(string testName)
    {
        if (_testDirectories.TryRemove(testName, out var directory))
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(directory))
                {
                    try
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't throw - cleanup is best effort
                        Console.WriteLine($"Failed to cleanup test directory {directory}: {ex.Message}");
                    }
                }
            }).ConfigureAwait(false);
        }
    }

    public async Task InitializeAsync()
    {
        await _directoryCreationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_rootTempPath);
        }
        finally
        {
            _directoryCreationLock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        if (_disposed) return;
        
        await CleanupAllDirectoriesAsync().ConfigureAwait(false);
        
        _directoryCreationLock?.Dispose();
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        // Synchronous cleanup - best effort
        CleanupAllDirectories();
        
        _directoryCreationLock?.Dispose();
        _disposed = true;
    }

    private async Task CleanupAllDirectoriesAsync()
    {
        var cleanupTasks = _testDirectories.Values
            .Select(dir => Task.Run(() => TryDeleteDirectory(dir)))
            .ToList();

        await Task.WhenAll(cleanupTasks).ConfigureAwait(false);

        // Finally, try to delete the root directory
        await Task.Run(() => TryDeleteDirectory(_rootTempPath)).ConfigureAwait(false);
        
        _testDirectories.Clear();
    }

    private void CleanupAllDirectories()
    {
        foreach (var dir in _testDirectories.Values)
        {
            TryDeleteDirectory(dir);
        }

        TryDeleteDirectory(_rootTempPath);
        _testDirectories.Clear();
    }

    private static void TryDeleteDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        try
        {
            // Ensure all files are not read-only before deletion
            var dirInfo = new DirectoryInfo(directory);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.Normal;
            }

            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            // Log but don't throw - cleanup is best effort
            Console.WriteLine($"Failed to delete directory {directory}: {ex.Message}");
        }
    }

    private static string SanitizeTestName(string testName)
    {
        // Remove invalid path characters and limit length
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", testName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        
        // Limit to reasonable length
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100);
        }

        // Add a short hash for uniqueness
        var hash = testName.GetHashCode().ToString("X8");
        return $"{sanitized}_{hash}";
    }
}