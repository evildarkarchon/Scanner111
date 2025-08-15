namespace Scanner111.Tests.TestHelpers;

/// <summary>
///     Test collection definitions for managing parallel test execution.
///     Tests in the same collection run sequentially, different collections run in parallel.
/// </summary>
/// <summary>
///     Collection for tests that perform heavy I/O operations.
///     These tests run sequentially to avoid file system conflicts.
///     Includes: Integration tests, concurrent file operations, report writing tests
/// </summary>
[CollectionDefinition("IO Heavy Tests")]
public class IOHeavyTestCollection : ICollectionFixture<IOTestFixture>
{
}

/// <summary>
///     Collection for FileSystemWatcher tests.
///     These tests run sequentially to avoid watcher conflicts and file monitoring issues.
///     Includes: WatchCommand tests, file monitoring tests, dashboard tests
/// </summary>
[CollectionDefinition("FileWatcher Tests")]
public class FileWatcherTestCollection : ICollectionFixture<FileWatcherTestFixture>
{
}

/// <summary>
///     Collection for database tests.
///     These tests may share database resources and should run sequentially.
///     Includes: StatisticsService tests, FormIdDatabaseService tests, SQLite operations
/// </summary>
[CollectionDefinition("Database Tests")]
public class DatabaseTestCollection : ICollectionFixture<DatabaseTestFixture>
{
}

// Note: SettingsTestCollection is defined in SettingsTestCollection.cs
// to avoid duplicate definitions. It uses the same "Settings Tests" collection name.

/// <summary>
///     Collection for tests involving backup and restore operations.
///     These tests run sequentially to avoid conflicts in backup directories.
///     Includes: BackupService tests, unsolved logs mover tests
/// </summary>
[CollectionDefinition("Backup Tests")]
public class BackupTestCollection : ICollectionFixture<BackupTestFixture>
{
}

/// <summary>
///     Collection for tests that interact with mod managers and game installations.
///     These tests run sequentially to avoid conflicts with mod manager state.
///     Includes: MO2 tests, Vortex tests, ModManagerService tests, game path detection
/// </summary>
[CollectionDefinition("ModManager Tests")]
public class ModManagerTestCollection : ICollectionFixture<ModManagerTestFixture>
{
}

/// <summary>
///     Collection for tests that use Spectre.Console terminal UI.
///     These tests run sequentially to avoid console output conflicts.
///     Includes: SpectreMessageHandler, SpectreTerminalUIService, Interactive mode tests
/// </summary>
[CollectionDefinition("Terminal UI Tests")]
public class TerminalUITestCollection : ICollectionFixture<TerminalUITestFixture>
{
}

/// <summary>
///     Collection for tests that make HTTP/network calls.
///     These tests run sequentially to avoid network resource conflicts.
///     Includes: UpdateService tests, HTTP client tests
/// </summary>
[CollectionDefinition("Network Tests")]
public class NetworkTestCollection : ICollectionFixture<NetworkTestFixture>
{
}

/// <summary>
///     Collection for GUI/Avalonia tests.
///     These tests run sequentially to avoid UI thread conflicts.
///     Includes: ViewModels, Converters, GUI services, Theme tests
/// </summary>
[CollectionDefinition("GUI Tests")]
public class GUITestCollection : ICollectionFixture<GUITestFixture>
{
}

/// <summary>
///     Collection for audio notification tests.
///     These tests run sequentially to avoid audio system conflicts.
///     Includes: AudioNotificationService tests
/// </summary>
[CollectionDefinition("Audio Tests")]
public class AudioTestCollection : ICollectionFixture<AudioTestFixture>
{
}

/// <summary>
///     Collection for crash log parsing and analysis tests.
///     These tests run sequentially when they access shared test data files.
///     Includes: CrashLogParser tests with shared sample files
/// </summary>
[CollectionDefinition("Parser Tests")]
public class ParserTestCollection : ICollectionFixture<ParserTestFixture>
{
}

/// <summary>
///     Collection for pipeline integration tests.
///     These tests run sequentially as they test the full analysis pipeline.
///     Includes: ScanPipeline, EnhancedScanPipeline, FCX pipeline tests
/// </summary>
[CollectionDefinition("Pipeline Tests")]
public class PipelineTestCollection : ICollectionFixture<PipelineTestFixture>
{
}

/// <summary>
///     Shared fixture for I/O heavy tests.
/// </summary>
public class IOTestFixture : IDisposable
{
    public IOTestFixture()
    {
        TempDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_IOTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(TempDirectory);
    }

    public string TempDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(TempDirectory))
                Directory.Delete(TempDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
///     Shared fixture for FileSystemWatcher tests.
/// </summary>
public class FileWatcherTestFixture : IDisposable
{
    public FileWatcherTestFixture()
    {
        WatchDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_WatchTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(WatchDirectory);
    }

    public string WatchDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WatchDirectory))
                Directory.Delete(WatchDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
///     Shared fixture for database tests.
/// </summary>
public class DatabaseTestFixture : IDisposable
{
    public DatabaseTestFixture()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Scanner111_DbTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        DatabasePath = Path.Combine(tempDir, "test.db");
    }

    public string DatabasePath { get; }

    public void Dispose()
    {
        try
        {
            var dir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
///     Shared fixture for settings tests.
/// </summary>
public class SettingsTestFixture : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();

    public SettingsTestFixture()
    {
        // Store original environment variables that might be modified
        _originalEnvironmentVariables["SCANNER111_SETTINGS_PATH"] =
            Environment.GetEnvironmentVariable("SCANNER111_SETTINGS_PATH");
        _originalEnvironmentVariables["SCANNER111_CONFIG_DIR"] =
            Environment.GetEnvironmentVariable("SCANNER111_CONFIG_DIR");
    }

    public void Dispose()
    {
        // Restore original environment variables
        foreach (var kvp in _originalEnvironmentVariables) Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }
}

/// <summary>
///     Shared fixture for backup tests.
/// </summary>
public class BackupTestFixture : IDisposable
{
    public BackupTestFixture()
    {
        BackupDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_BackupTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(BackupDirectory);
    }

    public string BackupDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(BackupDirectory))
                Directory.Delete(BackupDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
///     Shared fixture for mod manager tests.
/// </summary>
public class ModManagerTestFixture : IDisposable
{
    public ModManagerTestFixture()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"Scanner111_ModTests_{Guid.NewGuid()}");
        TestModsDirectory = Path.Combine(baseDir, "mods");
        TestProfilesDirectory = Path.Combine(baseDir, "profiles");
        Directory.CreateDirectory(TestModsDirectory);
        Directory.CreateDirectory(TestProfilesDirectory);
    }

    public string TestModsDirectory { get; }
    public string TestProfilesDirectory { get; }

    public void Dispose()
    {
        try
        {
            var baseDir = Path.GetDirectoryName(TestModsDirectory);
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
///     Shared fixture for terminal UI tests.
/// </summary>
public class TerminalUITestFixture : IDisposable
{
    public void Dispose()
    {
        // Reset console state if needed
    }
}

/// <summary>
///     Shared fixture for network tests.
/// </summary>
public class NetworkTestFixture : IDisposable
{
    public void Dispose()
    {
        // Clean up network resources
    }
}

/// <summary>
///     Shared fixture for GUI tests.
/// </summary>
public class GUITestFixture : IDisposable
{
    public void Dispose()
    {
        // Clean up UI resources
    }
}

/// <summary>
///     Shared fixture for audio tests.
/// </summary>
public class AudioTestFixture : IDisposable
{
    public void Dispose()
    {
        // Clean up audio resources
    }
}

/// <summary>
///     Shared fixture for parser tests.
/// </summary>
public class ParserTestFixture : IDisposable
{
    public ParserTestFixture()
    {
        SampleLogsDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_ParserTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(SampleLogsDirectory);
    }

    public string SampleLogsDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(SampleLogsDirectory))
                Directory.Delete(SampleLogsDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
///     Shared fixture for pipeline tests.
/// </summary>
public class PipelineTestFixture : IDisposable
{
    public PipelineTestFixture()
    {
        TestDataDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_PipelineTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(TestDataDirectory);
    }

    public string TestDataDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(TestDataDirectory))
                Directory.Delete(TestDataDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}