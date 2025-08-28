namespace Scanner111.Test.Infrastructure.TestFixtures;

/// <summary>
///     Test fixture that provides a temporary directory for test isolation.
///     Shared across test classes to reduce directory creation overhead.
/// </summary>
public class TempDirectoryFixture : IAsyncLifetime
{
    private readonly List<string> _createdDirectories = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public string RootDirectory { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        RootDirectory = Path.Combine(
            Path.GetTempPath(), 
            $"Scanner111_TestRun_{Guid.NewGuid():N}");
        
        Directory.CreateDirectory(RootDirectory);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Cleanup all created test directories
            foreach (var dir in _createdDirectories.Where(Directory.Exists))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to cleanup directory {dir}: {ex.Message}");
                }
            }

            // Cleanup root directory
            if (Directory.Exists(RootDirectory))
            {
                try
                {
                    Directory.Delete(RootDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to cleanup root directory {RootDirectory}: {ex.Message}");
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Creates a test-specific subdirectory.
    /// </summary>
    public async Task<string> CreateTestDirectoryAsync(string testName)
    {
        await _lock.WaitAsync();
        try
        {
            var sanitizedName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
            var testDir = Path.Combine(RootDirectory, $"{sanitizedName}_{Guid.NewGuid():N}");
            
            Directory.CreateDirectory(testDir);
            _createdDirectories.Add(testDir);
            
            return testDir;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Creates a test file in the specified directory.
    /// </summary>
    public async Task<string> CreateTestFileAsync(
        string directory, 
        string fileName, 
        string content)
    {
        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }

    /// <summary>
    ///     Creates a test file with sample crash log content.
    /// </summary>
    public async Task<string> CreateSampleCrashLogAsync(string directory, string? fileName = null)
    {
        fileName ??= $"crash-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log";
        
        var content = @"Buffout 4 v1.30.0
Unhandled exception at 0x7FF6B1234567

SYSTEM INFO:
OS: Windows 11

PLUGINS:
[00] Fallout4.esm
[01] DLCRobot.esm
[FE:001] TestMod.esp

CALL STACK:
[0] 0x7FF6B1234567 Fallout4.exe+0x1234567
[1] 0x7FF6B1234568 TestMod.esp+0x100

End of log";
        
        return await CreateTestFileAsync(directory, fileName, content);
    }
}

/// <summary>
///     Collection definition for tests that share the temp directory fixture.
/// </summary>
[CollectionDefinition("TempDirectory")]
public class TempDirectoryCollection : ICollectionFixture<TempDirectoryFixture>
{
    // This class is never instantiated, it's just for xUnit metadata
}