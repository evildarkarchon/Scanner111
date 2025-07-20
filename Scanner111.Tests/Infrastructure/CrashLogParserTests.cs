using System.Text;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class CrashLogParserTests : IDisposable
{
    private readonly string _testDirectory;
    
    public CrashLogParserTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }
    
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
    
    [Fact]
    public async Task ParseAsync_HandlesIncompleteLogCorrectly()
    {
        // Arrange
        // Create a log with the PLUGINS section but no actual plugins listed
        // Remove the newline before first plugin and all plugin lines
        var incompleteLog = GenerateValidCrashLog().Replace(
            "	[00:000]   Fallout4.esm\n	[01:000]   DLCRobot.esm\n	[02:001]   DLCworkshop01.esm\n	[FE:001]   TestPlugin.esp\n",
            "");
        var crashLogPath = CreateTestCrashLog("incomplete.log", incompleteLog);
        
        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsIncomplete);
    }
    
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
        while (lines.Count < 25)
        {
            lines.Add("");
        }
        
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
    
    [Fact]
    public async Task ParseAsync_WithCancellationToken_RespectsCanellation()
    {
        // Arrange
        var crashLogPath = CreateTestCrashLog("cancel_test.log", GenerateValidCrashLog());
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CrashLogParser.ParseAsync(crashLogPath, cts.Token));
    }
    
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
        invalidBytes[100] = 0xFF;  // Invalid UTF-8 start byte
        invalidBytes[101] = 0xFE;  // Invalid UTF-8 sequence
        invalidBytes[102] = 0xFD;  // Invalid UTF-8 sequence
        Array.Copy(bytes, 100, invalidBytes, 103, bytes.Length - 100);
        
        await File.WriteAllBytesAsync(crashLogPath, invalidBytes);
        
        // Act
        var result = await CrashLogParser.ParseAsync(crashLogPath);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.OriginalLines);
    }
    
    // Helper methods
    private string CreateTestCrashLog(string filename, string content)
    {
        var filePath = Path.Combine(_testDirectory, filename);
        File.WriteAllText(filePath, content);
        return filePath;
    }
    
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
    
    // Cleanup
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}