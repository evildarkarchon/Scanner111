using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class CrashLogParserAdditionalTests
{
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
            Assert.NotNull(result);
            Assert.Empty(result.Plugins);
            Assert.True(result.IsIncomplete, "Crash log with no plugins should be marked as incomplete");
            Assert.Equal("Fallout 4 v1.10.163", result.GameVersion);
            Assert.Equal("Buffout 4 v1.26.2", result.CrashGenVersion);
            Assert.NotEmpty(result.XseModules);
            Assert.NotEmpty(result.CallStack);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

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
            Assert.NotNull(result);
            Assert.Equal(2, result.Plugins.Count);
            Assert.False(result.IsIncomplete);
            Assert.True(result.Plugins.ContainsKey("Fallout4.esm"));
            Assert.True(result.Plugins.ContainsKey("DLCRobot.esm"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}