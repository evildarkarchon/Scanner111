using System.Text;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Unit test suite for validating the functionality of the CrashLogParser class.
/// </summary>
/// <remarks>
/// The tests in this class focus on verifying various parsing scenarios, ensuring
/// correct handling of different types of crash logs, and validating proper error
/// handling during parsing operations.
/// </remarks>
/// <example>
/// Contains tests that cover cases such as:
/// - Parsing valid crash log files.
/// - Handling invalid or incomplete log files.
/// - Extracting specific details such as versions, error messages, plugins, and more.
/// - Gracefully handling edge cases, such as missing or malformed data.
/// - Ensuring cancellation token respect.
/// </example>
/// <note>
/// Each test ensures that CrashLogParser produces the expected output or behavior
/// under different conditions, ensuring robustness and reliability of the parser.
/// Temporary files created during testing are cleaned up after each test.
/// </note>
public class CrashLogParserTests : IDisposable
{
    private readonly string _testDirectory;

    public CrashLogParserTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// Releases the resources used by the CrashLogParserTests class.
    /// This method is called to clean up resources, such as temporary directories,
    /// that were allocated during the tests. Any cleanup operation errors will
    /// be ignored. Additionally, this method suppresses finalization for the current
    /// object to optimize garbage collection.
    /// Implements the IDisposable interface.
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }

        GC.SuppressFinalize(this);
    }

    /// Verifies that the ParseAsync method processes a valid crash log file correctly
    /// and ensures the returned CrashLog instance contains the expected number of lines
    /// and the correct file path.
    /// This test ensures that when a valid crash log is provided, the parser extracts
    /// the relevant information without errors and returns a non-null, correctly
    /// populated result.
    /// <returns>Asserts that the returned CrashLog is not null, has the correct file
    /// path, and contains non-empty original lines.</returns>
    [Fact]
    public async Task ParseAsync_WithValidCrashLog_ReturnsCorrectlySized()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("valid_crash.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(crashLogPath, result.FilePath);
        Assert.NotEmpty(result.OriginalLines);
    }

    /// Tests the behavior of the CrashLogParser.ParseAsync method when provided with
    /// a crash log file that is too short to contain valid crash information.
    /// The method should return null if the file does not meet the minimum length
    /// required for parsing.
    /// <returns>
    /// A null value to indicate that the crash log file is too short to be parsed.
    /// </returns>
    [Fact]
    public async Task ParseAsync_WithTooShortFile_ReturnsNull()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("short_crash.log",
            string.Join("\n", Enumerable.Range(1, 10).Select(i => $"Line {i}")));

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.Null(result);
    }

    /// Tests the behavior of the ParseAsync method when provided with a path to a non-existent file.
    /// This test verifies that the method correctly returns null when the specified file does not exist.
    /// Ensures the method handles missing files gracefully and avoids exceptions.
    [Fact]
    public async Task ParseAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "nonexistent.log");

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.Null(result);
    }

    /// Verifies that the ParseAsync method correctly parses the game version
    /// from a valid crash log file. This test ensures that the GameVersion
    /// property in the returned CrashLog object matches the expected value.
    /// <returns>Task representing the asynchronous operation of the test.</returns>
    [Fact]
    public async Task ParseAsync_ParsesGameVersionCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("game_version.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallout 4 v1.10.163", result.GameVersion);
    }

    /// Verifies that the ParseAsync method correctly parses the "CrashGenVersion" field from a valid crash log.
    /// This test ensures that the returned CrashLog object contains the expected value for the CrashGenVersion property.
    /// The input crash log file is generated with valid contents, and the test validates the parsing capability.
    /// <returns>
    /// Asserts that the CrashGenVersion property of the parsed CrashLog object matches the expected value.
    /// </returns>
    [Fact]
    public async Task ParseAsync_ParsesCrashGenVersionCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("crashgen_version.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Buffout 4 v1.26.2", result.CrashGenVersion);
    }

    /// Verifies that the ParseAsync method parses the main error section
    /// of a crash log file correctly. Ensures that the result contains
    /// the expected main error message, including specific keywords
    /// and details such as memory addresses or exceptions.
    /// Useful for validating the parser's ability to extract
    /// the main error information accurately.
    /// <returns>Returns a task representing the asynchronous operation. The result includes a
    /// deserialized CrashLog object whose MainError property correctly reflects the
    /// extracted main error content.</returns>
    [Fact]
    public async Task ParseAsync_ParsesMainErrorCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("main_error.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Unhandled exception", result.MainError);
        Assert.Contains("at 0x7FF798889DFA", result.MainError);
    }

    /// Validates that the crash log parser correctly identifies and processes plugins from a crash log file.
    /// This test checks the proper parsing of plugin data, including plugin names and their associated versions,
    /// ensuring that the parsed data matches expected values.
    /// <returns>Successfully verifies that plugin data in a crash log is parsed accurately.</returns>
    [Fact]
    public async Task ParseAsync_ParsesPluginsCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("plugins.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Plugins);
        Assert.True(result.Plugins.ContainsKey("Fallout4.esm"));
        Assert.Equal("00:000", result.Plugins["Fallout4.esm"]);
        Assert.True(result.Plugins.ContainsKey("DLCRobot.esm"));
        Assert.Equal("01:000", result.Plugins["DLCRobot.esm"]);
    }

    /// Validates the parsing of XSE plugin modules from a crash log file.
    /// This test ensures that the `ParseAsync` method correctly extracts
    /// the modules listed in the provided crash log file.
    /// Verifies that the parsed modules include expected entries
    /// such as filenames of dynamically loaded plugins.
    /// <returns> A task that represents the asynchronous test, confirming
    /// the successful parsing of the XSE modules.</returns>
    [Fact]
    public async Task ParseAsync_ParsesXseModulesCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("xse_modules.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.XseModules);
        Assert.Contains("f4se_1_10_163.dll", result.XseModules);
        Assert.Contains("buffout4.dll", result.XseModules);
    }

    /// Validates that the `ParseAsync` method correctly parses crash generation settings
    /// from the provided crash log. This includes verifying that key-value pairs in the
    /// `CrashgenSettings` dictionary match the expected data, such as specific plugin versions
    /// or settings. Ensures that the method handles settings with mixed types and nested data
    /// structures appropriately.
    /// <returns>Task representing the asynchronous unit test operation that validates the parsing
    /// of crash generation settings in a crash log.</returns>
    [Fact]
    public async Task ParseAsync_ParsesCrashgenSettingsCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("settings.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.CrashgenSettings);
        Assert.True(result.CrashgenSettings.ContainsKey("F4EE"));
        Assert.Equal(true, result.CrashgenSettings["F4EE"]);
        Assert.True(result.CrashgenSettings.ContainsKey("Buffout4"));
        Assert.Equal(1, result.CrashgenSettings["Buffout4"]);
    }

    /// Verifies that the ParseAsync method correctly handles crash logs that lack complete sections,
    /// such as a PLUGINS section without any plugin entries.
    /// This test ensures that the result is not null and validates that the IsIncomplete property
    /// is set to true when the log is partially incomplete.
    /// This behavior is critical for robust handling of edge cases in crash log parsing.
    [Fact]
    public async Task ParseAsync_HandlesIncompleteLogCorrectly()
    {
        // Arrange
        // Create a log with the PLUGINS section header but no actual plugin entries
        var originalLog = GenerateValidCrashLog();
        var pluginsIndex = originalLog.IndexOf("PLUGINS:", StringComparison.OrdinalIgnoreCase);
        string incompleteLog;

        if (pluginsIndex >= 0)
        {
            // Find and remove all plugin entries (lines starting with [XX:YYY])
            var lines = originalLog.Split('\n');
            var filteredLines = lines.Where(line => !line.TrimStart().StartsWith("[") || !line.Contains("]") || !line.Contains(".es")).ToList();

            incompleteLog = string.Join("\n", filteredLines);
        }
        else
        {
            incompleteLog = originalLog;
        }

        var crashLogPath = CreateTestCrashLog("incomplete.log", incompleteLog);

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsIncomplete);
    }

    /// Verifies that the call stack is correctly extracted from a crash log using the ParseAsync method.
    /// This test ensures that the CallStack property in the resulting CrashLog object includes
    /// the expected entries from the parsed crash log file.
    /// <returns>
    /// A task representing the asynchronous operation. The task result will validate that:
    /// - The returned CrashLog object is not null.
    /// - The CallStack contains at least one entry.
    /// - The CallStack includes a specific entry, such as "Fallout4.exe+2479DFA".
    /// </returns>
    [Fact]
    public async Task ParseAsync_ExtractsCallStackCorrectly()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("callstack.log", GenerateValidCrashLog());

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.CallStack);
        Assert.Contains("Fallout4.exe+2479DFA", string.Join("\n", result.CallStack));
    }

    /// Verifies that the parser correctly processes a crash log specific to Skyrim Special Edition.
    /// This test validates that the game version, Crash Logger version, and relevant SKSE modules
    /// are accurately extracted from the provided crash log data.
    /// <returns>A completed task upon successful test execution.</returns>
    [Fact]
    public async Task ParseAsync_HandlesSkyrimLogCorrectly()
    {
        // Arrange
        var skyrimLog = GenerateSkyrimCrashLog();
        var crashLogPath = CreateTestCrashLog("skyrim.log", skyrimLog);

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Skyrim SE v1.5.97", result.GameVersion);
        Assert.Equal("Crash Logger SSE v1.0", result.CrashGenVersion);
        Assert.NotEmpty(result.XseModules);
        // Should have extracted from SKSE PLUGINS section
        Assert.Contains("skse64_1_5_97.dll", result.XseModules);
    }

    /// Tests that the `ParseAsync` method correctly handles crash log entries containing a pipe ('|')
    /// in the main error message. Verifies that the `MainError` field in the parsed result includes
    /// all components of the error message, including segments following the pipe character.
    /// Ensures that the method accurately processes and retains both the exception details
    /// and the piped information within the `MainError` field.
    /// <returns>Asserts that the parsed `CrashLog.MainError` is not null and contains the expected
    /// details, including the pipe-separated values.</returns>
    [Fact]
    public async Task ParseAsync_HandlesMainErrorWithPipeCorrectly()
    {
        // Arrange
        var content = GenerateValidCrashLog().Replace(
            "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\" at 0x7FF798889DFA",
            "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\" at 0x7FF798889DFA | Fallout4.exe+2479DFA");
        var crashLogPath = CreateTestCrashLog("pipe_error.log", content);

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Unhandled exception", result.MainError);
        Assert.Contains("\n", result.MainError);
        Assert.Contains("Fallout4.exe+2479DFA", result.MainError);
    }

    /// Verifies that the ParseAsync method correctly handles crash logs with missing segments
    /// while maintaining a valid format. This test ensures that the parser does not fail
    /// or produce incorrect results when certain sections of the crash log, such as plugins,
    /// are empty or incomplete. Additionally, it validates the proper handling of other
    /// sections such as call stack and modules, and ensures the parsed result is marked
    /// as incomplete where appropriate.
    /// <returns>Task representing the asynchronous operation, used for verification
    /// of the test's assertions.</returns>
    [Fact]
    public async Task ParseAsync_HandlesEmptySegmentsGracefully()
    {
        // Arrange - Create a log with missing segments but valid format
        var content = @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF798889DFA

	[Compatibility]
	F4EE: true
SYSTEM SPECS:
	OS: Windows 10
PROBABLE CALL STACK:
	[0] 0x7FF798889DFA
MODULES:
	Fallout4.exe
F4SE PLUGINS:
	f4se.dll
PLUGINS:
";
        var lines = new List<string>(content.Split('\n'));
        // Ensure we have at least 20 lines
        while (lines.Count < 25) lines.Add("");

        var crashLogPath = CreateTestCrashLog("empty_segments.log", string.Join("\n", lines));

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Plugins); // No plugins listed in the PLUGINS section
        Assert.NotEmpty(result.XseModules); // But has XSE modules
        Assert.NotEmpty(result.CallStack); // And has call stack
        Assert.True(result.IsIncomplete); // Marked incomplete due to no plugins
    }

    /// Verifies that the `ParseAsync` method accurately parses mixed case settings
    /// present in the crash log file. This test checks whether values, regardless of their case,
    /// are mapped correctly to the `CrashgenSettings` dictionary with the appropriate data types.
    /// <returns>
    /// A task that represents the asynchronous operation. If parsing is successful,
    /// the parsed CrashLog object is returned containing the settings with correct values.
    /// Fails the test if the parsing result does not meet the expectations.
    /// </returns>
    [Fact]
    public async Task ParseAsync_ParsesMixedCaseSettingsCorrectly()
    {
        // Arrange
        var crashLog = GenerateValidCrashLog().Replace(
            "F4EE: true",
            "F4EE: TRUE\n\tAutoTimer: FALSE\n\tMaxStack: 100");
        var crashLogPath = CreateTestCrashLog("mixed_settings.log", crashLog);

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(true, result.CrashgenSettings["F4EE"]);
        Assert.Equal(false, result.CrashgenSettings["AutoTimer"]);
        Assert.Equal(100, result.CrashgenSettings["MaxStack"]);
    }

    /// Verifies that the ParseAsync method correctly handles XSE modules listed in a crash log
    /// when some modules do not have version information specified. This method ensures that
    /// all such modules are still included in the parsed results without skipping or misinterpreting them.
    /// This test arranges a crash log with mixed versioned and non-versioned XSE modules, invokes the parser,
    /// and asserts the presence of all intended modules in the result.
    /// <returns>
    /// Does not return a value as it is a unit test. The test asserts the presence of
    /// correctly parsed XSE modules in the CrashLog object produced by the ParseAsync method.
    /// </returns>
    [Fact]
    public async Task ParseAsync_HandlesXseModulesWithoutVersions()
    {
        // Arrange
        var crashLog = GenerateValidCrashLog().Replace(
            "f4se_1_10_163.dll v2.0.17",
            "f4se_1_10_163.dll\n\tsome_plugin.dll\n\tanother.dll v1.0");
        var crashLogPath = CreateTestCrashLog("xse_no_version.log", crashLog);

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("f4se_1_10_163.dll", result.XseModules);
        Assert.Contains("some_plugin.dll", result.XseModules);
        Assert.Contains("another.dll", result.XseModules);
    }

    /// Validates that the ParseAsync method respects the cancellation token and halts its operation
    /// when cancellation is requested.
    /// This test initiates the parsing of a crash log file and ensures that an OperationCanceledException
    /// is thrown if the provided CancellationToken is canceled before or during execution.
    /// <returns>
    /// A task representing the asynchronous operation of the test. This task will fail if
    /// the ParseAsync method does not correctly handle the canceled token or if it does
    /// not throw the expected OperationCanceledException.
    /// </returns>
    [Fact]
    public async Task ParseAsync_WithCancellationToken_RespectsCanellation()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("cancel_test.log", GenerateValidCrashLog());
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CrashLogParser.ParseAsync(crashLogPath, cts.Token));
    }

    /// Tests the ability of the ParseAsync method to handle crash logs containing invalid UTF-8 byte sequences.
    /// This method verifies that the parser correctly processes a file with invalid UTF-8 encoding,
    /// ensures the result is not null, and asserts that the original lines of the crash log are populated.
    /// The test method creates a crash log file with a mix of valid and invalid UTF-8 bytes, simulating corrupted data.
    /// It then invokes the ParseAsync method and validates that the improper encoding does not prevent the parser
    /// from generating a valid CrashLog instance or retaining the recoverable sections of the log.
    /// <returns>A Task representing the asynchronous operation of the test.</returns>
    [Fact]
    public async Task ParseAsync_HandlesUtf8WithErrors()
    {
        // Arrange - Create a file with invalid UTF-8 sequences
        var crashLogPath = Path.Combine(_testDirectory, "utf8_error.log");
        var content = GenerateValidCrashLog();
        var bytes = Encoding.UTF8.GetBytes(content);

        // Insert some invalid UTF-8 bytes
        var invalidBytes = new byte[bytes.Length + 3];
        Array.Copy(bytes, invalidBytes, 100);
        invalidBytes[100] = 0xFF; // Invalid UTF-8 start byte
        invalidBytes[101] = 0xFE; // Invalid UTF-8 sequence
        invalidBytes[102] = 0xFD; // Invalid UTF-8 sequence
        Array.Copy(bytes, 100, invalidBytes, 103, bytes.Length - 100);

        await File.WriteAllBytesAsync(crashLogPath, invalidBytes);

        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.OriginalLines);
    }

    // Helper methods
    /// Creates a test crash log file with the specified filename and content.
    /// <param name="filename">
    /// The name of the crash log file to be created.
    /// </param>
    /// <param name="content">
    /// The content to be written into the crash log file.
    /// </param>
    /// <returns>
    /// The full file path of the created test crash log file.
    /// </returns>
    private string CreateTestCrashLog(string filename, string content)
    {
        var filePath = Path.Combine(_testDirectory, filename);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// Generates a valid crash log string for testing purposes.
    /// The generated log contains mock data simulating a typical
    /// crash log structure, including game version information,
    /// system specifications, error details, and modules loaded.
    /// <returns>A string representing a valid crash log.</returns>
    private string GenerateValidCrashLog()
    {
        return @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF798889DFA

	[Compatibility]
	F4EE: true
	Buffout4: 1
	
SYSTEM SPECS:
	OS: Microsoft Windows 10 Pro v10.0.19044
	CPU: GenuineIntel 11th Gen Intel(R) Core(TM) i7-11700K @ 3.60GHz
	GPU: NVIDIA GeForce RTX 3080
	
PROBABLE CALL STACK:
	[0] 0x7FF798889DFA Fallout4.exe+2479DFA
	[1] 0x7FF7988899FF Fallout4.exe+24799FF
	[2] 0x7FF798889912 Fallout4.exe+2479912
	
MODULES:
	Fallout4.exe
	KERNEL32.DLL
	ntdll.dll
	
F4SE PLUGINS:
	f4se_1_10_163.dll v2.0.17
	buffout4.dll v1.26.2
	
PLUGINS:
	[00:000]   Fallout4.esm
	[01:000]   DLCRobot.esm
	[02:001]   DLCworkshop01.esm
	[FE:001]   TestPlugin.esp
";
    }

    /// Generates a sample crash log for Skyrim Special Edition.
    /// This method produces a simulated crash log string, including game version,
    /// system specifications, probable call stack, loaded modules, SKSE plugins,
    /// and game plugins. The generated log is designed to test parsing functionality
    /// and ensure compatibility with different structures and contents of crash logs.
    /// <returns>
    /// A string representing the simulated crash log for Skyrim Special Edition.
    /// </returns>
    private string GenerateSkyrimCrashLog()
    {
        return @"Skyrim SE v1.5.97
Crash Logger SSE v1.0

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF798889DFA

	[Compatibility]
	SKSE: true
	
SYSTEM SPECS:
	OS: Microsoft Windows 10 Pro v10.0.19044
	CPU: GenuineIntel 11th Gen Intel(R) Core(TM) i7-11700K @ 3.60GHz
	GPU: NVIDIA GeForce RTX 3080
	
PROBABLE CALL STACK:
	[0] 0x7FF798889DFA SkyrimSE.exe+2479DFA
	
MODULES:
	SkyrimSE.exe
	KERNEL32.DLL
	
SKSE PLUGINS:
	skse64_1_5_97.dll
	powerofthree_papyrusextender.dll
	
PLUGINS:
	[00:000]   Skyrim.esm
	[01:000]   Update.esm
";
    }
}