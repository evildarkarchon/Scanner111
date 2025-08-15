using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Provides additional test cases for the CrashLogParser class.
/// </summary>
/// <remarks>
///     This class contains unit tests for verifying the parsing logic of crash logs,
///     including scenarios such as logs with no plugins and logs containing empty lines.
/// </remarks>
[Collection("Parser Tests")]
public class CrashLogParserAdditionalTests
{
	/// <summary>
	///     Verifies that the crash log parser correctly identifies a log with no plugins
	///     and marks the result as incomplete.
	/// </summary>
	/// <returns>
	///     A task representing the asynchronous operation. The task result
	///     is a parsed <see cref="CrashLog" /> instance indicating the log details,
	///     including whether the log is incomplete and its associated metadata.
	/// </returns>
	[Fact]
    public async Task ParseAsync_CrashLogWithNoPlugins_IsMarkedIncomplete()
    {
        // Arrange
        const string emptyPluginsLog = @"Fallout 4 v1.10.163
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
";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, emptyPluginsLog);

        try
        {
            // Act
            var result = await CrashLogParser.ParseAsync(tempFile);

            // Assert
            result.Should().NotBeNull();
            result.Plugins.Should().BeEmpty();
            result.IsIncomplete.Should().BeTrue("crash log with no plugins should be marked as incomplete");
            result.GameVersion.Should().Be("Fallout 4 v1.10.163");
            result.CrashGenVersion.Should().Be("Buffout 4 v1.26.2");
            result.XseModules.Should().NotBeEmpty();
            result.CallStack.Should().NotBeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

	/// <summary>
	///     Verifies that the crash log parser correctly processes a log containing empty lines,
	///     ensuring they are ignored during the parsing process.
	/// </summary>
	/// <returns>
	///     A task representing the asynchronous operation. The task result
	///     is a parsed <see cref="CrashLog" /> instance that excludes empty lines
	///     and correctly identifies the list of plugins.
	/// </returns>
	[Fact]
    public async Task ParseAsync_CrashLogWithEmptyLines_IgnoresEmptyLines()
    {
        // Arrange
        var logWithEmptyLines = @"Fallout 4 v1.10.163
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


	[00:000]   Fallout4.esm


	[01:000]   DLCRobot.esm
";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logWithEmptyLines);

        try
        {
            // Act
            var result = await CrashLogParser.ParseAsync(tempFile);

            // Assert
            result.Should().NotBeNull();
            result.Plugins.Should().HaveCount(2);
            result.IsIncomplete.Should().BeFalse();
            result.Plugins.Should().ContainKey("Fallout4.esm");
            result.Plugins.Should().ContainKey("DLCRobot.esm");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}