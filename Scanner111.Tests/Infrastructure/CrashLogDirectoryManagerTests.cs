using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Test suite for verifying the functionality of the CrashLogDirectoryManager class.
///     Validates methods responsible for managing crash log directories, detecting game types,
///     and handling crash log files effectively.
/// </summary>
[Collection("IO Heavy Tests")]
public class CrashLogDirectoryManagerTests
{
    /// <summary>
    ///     Tests the GetDefaultCrashLogsDirectory method to verify it returns a valid and
    ///     non-empty path pointing to the default crash logs directory.
    ///     Ensures the path includes expected directory names like "Scanner111" and "Crash Logs".
    /// </summary>
    [Fact]
    public void GetDefaultCrashLogsDirectory_ReturnsValidPath()
    {
        var result = CrashLogDirectoryManager.GetDefaultCrashLogsDirectory();

        result.Should().NotBeNull("default directory path should be returned");
        result.Should().NotBeEmpty("directory path should not be empty");
        result.Should().Contain("Scanner111", "path should include Scanner111 directory");
        result.Should().Contain("Crash Logs", "path should include Crash Logs directory");
    }

    /// <summary>
    ///     Verifies that the DetectGameType method correctly identifies the game type based on the provided
    ///     game installation path. Tests various game paths to ensure that the correct game type is detected
    ///     and returned from the method.
    /// </summary>
    /// <param name="gamePath">
    ///     The file path to the game installation directory. This path typically contains the game's
    ///     executable file.
    /// </param>
    /// <param name="expectedType">
    ///     The expected game type to be detected, corresponding to the game installation directory
    ///     provided.
    /// </param>
    [Theory]
    [InlineData(@"C:\Games\Fallout4", "Fallout4")]
    [InlineData(@"C:\Games\Fallout4VR", "Fallout4VR")]
    [InlineData(@"C:\Games\SkyrimSE", "SkyrimSE")]
    public void DetectGameType_FromGamePath_ReturnsCorrectType(string gamePath, string expectedType)
    {
        // Create temporary directory structure using the provided gamePath as base
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(gamePath), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempPath);
            var exeName = expectedType switch
            {
                "Fallout4" => "Fallout4.exe",
                "Fallout4VR" => "Fallout4VR.exe",
                "SkyrimSE" => "SkyrimSE.exe",
                _ => "Fallout4.exe"
            };
            File.WriteAllText(Path.Combine(tempPath, exeName), "dummy");

            var result = CrashLogDirectoryManager.DetectGameType(tempPath);

            result.Should().Be(expectedType, "game type should be correctly detected");
        }
        finally
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }

    /// <summary>
    ///     Tests the DetectGameType method when no game path or crash log path is provided.
    ///     Verifies that the method defaults to returning "Fallout4",
    ///     which represents the expected default game type.
    /// </summary>
    [Fact]
    public void DetectGameType_NoGamePath_ReturnsDefaultFallout4()
    {
        var result = CrashLogDirectoryManager.DetectGameType();

        result.Should().Be("Fallout4", "default game type should be Fallout4");
    }

    /// <summary>
    ///     Tests the GetGameSpecificDirectory method to verify it correctly combines the base directory
    ///     and game type into a valid and expected path for storing game-specific crash logs.
    ///     Ensures the returned path adheres to the expected format with proper concatenation of input arguments.
    /// </summary>
    [Fact]
    public void GetGameSpecificDirectory_CombinesPathsCorrectly()
    {
        const string baseDir = "C:\\CrashLogs";
        const string gameType = "Fallout4";

        var result = CrashLogDirectoryManager.GetGameSpecificDirectory(baseDir, gameType);

        result.Should().Be("C:\\CrashLogs\\Fallout4", "paths should be combined correctly");
    }

    /// <summary>
    ///     Verifies that the EnsureDirectoryExists method successfully creates a directory
    ///     at the specified base path with the given game type as a subdirectory. Ensures
    ///     the method returns the correct path and that the directory is properly created
    ///     in the file system.
    /// </summary>
    [Fact]
    public void EnsureDirectoryExists_CreatesDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        const string gameType = "Fallout4";

        try
        {
            var result = CrashLogDirectoryManager.EnsureDirectoryExists(tempPath, gameType);

            Directory.Exists(result).Should().BeTrue("directory should be created");
            result.Should().Be(Path.Combine(tempPath, gameType), "correct path should be returned");
        }
        finally
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }

    /// <summary>
    ///     Verifies that the GetTargetPath method generates the correct target path based on the provided base directory,
    ///     game type, and original file name. Ensures the resulting path combines these elements appropriately
    ///     and that the necessary directories are created if they do not already exist.
    /// </summary>
    [Fact]
    public void GetTargetPath_ReturnsCorrectPath()
    {
        var tempBaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        const string gameType = "Fallout4";
        const string originalFile = "crash-2024-01-01-12-00-00.log";

        try
        {
            var result = CrashLogDirectoryManager.GetTargetPath(tempBaseDir, gameType, originalFile);

            var expectedPath = Path.Combine(tempBaseDir, gameType, originalFile);
            result.Should().Be(expectedPath, "target path should be correctly generated");
            Directory.Exists(Path.GetDirectoryName(result)).Should().BeTrue("parent directory should be created");
        }
        finally
        {
            if (Directory.Exists(tempBaseDir))
                Directory.Delete(tempBaseDir, true);
        }
    }

    /// <summary>
    ///     Validates the CopyCrashLog method to ensure it copies a specified crash log file
    ///     from a source directory to the correct target location within the base directory
    ///     under the provided game type. Verifies that the resulting file path is accurate,
    ///     the file exists at the target location, and its content matches the original file.
    /// </summary>
    [Fact]
    public void CopyCrashLog_CopiesFileToCorrectLocation()
    {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempBaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        const string fileName = "crash-2024-01-01-12-00-00.log";
        const string gameType = "Fallout4";

        try
        {
            // Create source file
            Directory.CreateDirectory(tempSourceDir);
            var sourceFile = Path.Combine(tempSourceDir, fileName);
            File.WriteAllText(sourceFile, "test crash log content");

            var result = CrashLogDirectoryManager.CopyCrashLog(sourceFile, tempBaseDir, gameType);

            var expectedPath = Path.Combine(tempBaseDir, gameType, fileName);
            result.Should().Be(expectedPath, "file should be copied to correct location");
            File.Exists(result).Should().BeTrue("copied file should exist");
            File.ReadAllText(result).Should().Be("test crash log content", "file content should match original");
        }
        finally
        {
            if (Directory.Exists(tempSourceDir))
                Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempBaseDir))
                Directory.Delete(tempBaseDir, true);
        }
    }

    /// <summary>
    ///     Verifies the CopyCrashLog method's ability to automatically detect the game type
    ///     from a source crash log file's contents and copy the file to the correct game-specific
    ///     directory under the provided base directory.
    ///     Ensures the returned path points to the expected location and the file is created
    ///     in the correct directory.
    /// </summary>
    [Fact]
    public void CopyCrashLog_AutoDetectsGameType()
    {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempBaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fileName = "crash-2024-01-01-12-00-00.log";

        try
        {
            // Create source file with fallout4 content
            Directory.CreateDirectory(tempSourceDir);
            var sourceFile = Path.Combine(tempSourceDir, fileName);
            File.WriteAllText(sourceFile, "System: Fallout4.exe version 1.10.984.0");

            var result = CrashLogDirectoryManager.CopyCrashLog(sourceFile, tempBaseDir);

            // Should auto-detect as Fallout4
            var expectedPath = Path.Combine(tempBaseDir, "Fallout4", fileName);
            result.Should().Be(expectedPath, "file should be copied to auto-detected game directory");
            File.Exists(result).Should().BeTrue("copied file should exist");
        }
        finally
        {
            if (Directory.Exists(tempSourceDir))
                Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempBaseDir))
                Directory.Delete(tempBaseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that the DetectGameType method correctly identifies the game type
    ///     based on the provided crash log paths associated with game-specific Scripting Extender (e.g., F4SE, SKSE).
    ///     Ensures accurate detection for Fallout4, Fallout4VR, and Skyrim Special Edition.
    /// </summary>
    [Fact]
    public void DetectGameType_FromXSEPath_ReturnsCorrectType()
    {
        // Test F4SE path detection
        const string f4SePath = @"C:\Users\Test\Documents\My Games\Fallout4\F4SE\crash-test.log";
        var result = CrashLogDirectoryManager.DetectGameType(crashLogPath: f4SePath);
        result.Should().Be("Fallout4", "F4SE path should be detected as Fallout4");

        // Test F4SE VR path detection
        const string f4SeVrPath = @"C:\Users\Test\Documents\My Games\Fallout4VR\F4SE\crash-test.log";
        result = CrashLogDirectoryManager.DetectGameType(crashLogPath: f4SeVrPath);
        result.Should().Be("Fallout4VR", "F4SE VR path should be detected as Fallout4VR");

        // Test SKSE SE path detection
        const string skseSePath = @"C:\Users\Test\Documents\My Games\Skyrim Special Edition\SKSE\crash-test.log";
        result = CrashLogDirectoryManager.DetectGameType(crashLogPath: skseSePath);
        result.Should().Be("SkyrimSE", "SKSE SE path should be detected as SkyrimSE");
    }

    /// <summary>
    ///     Tests the DetectGameType method using log content to verify it correctly identifies
    ///     and returns the expected game type based on the crash log content.
    ///     Validates the detection logic for specific game-related keywords within the log content.
    /// </summary>
    /// <param name="logContent">The content of the crash log as a string, which contains game-related information.</param>
    /// <param name="expectedType">The expected game type that should be returned based on the log content.</param>
    [Theory]
    [InlineData("SkyrimSE.exe detected", "SkyrimSE")]
    [InlineData("Skyrim Special Edition crashed", "SkyrimSE")]
    [InlineData("System Info: Fallout4VR.exe", "Fallout4VR")]
    public void DetectGameType_FromLogContent_ReturnsCorrectType(string logContent, string expectedType)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fileName = "crash-test.log";

        try
        {
            Directory.CreateDirectory(tempDir);
            var logFile = Path.Combine(tempDir, fileName);
            File.WriteAllText(logFile, logContent);

            var result = CrashLogDirectoryManager.DetectGameType(crashLogPath: logFile);

            result.Should().Be(expectedType, "game type should be correctly detected");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}