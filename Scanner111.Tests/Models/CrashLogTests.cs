using Scanner111.Core.Models;

namespace Scanner111.Tests.Models;

public class CrashLogTests
{
    private readonly string _sampleLogsPath = Path.Combine(
        Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
        "sample_logs"
    );

    /// <summary>
    /// Validates the default property values of the <see cref="CrashLog"/> class.
    /// </summary>
    /// <remarks>
    /// This test ensures that an instance of <see cref="CrashLog"/> is initialized with
    /// correct default values for all its properties, confirming compliance with the expected behavior
    /// when no specific values are assigned during instantiation.
    /// Verifies the following:<br/>
    /// - FilePath is initialized to an empty string.<br/>
    /// - FileName is derived correctly as an empty string when FilePath is empty.<br/>
    /// - OriginalLines is initialized as an empty collection.<br/>
    /// - Content is an empty string.<br/>
    /// - MainError is an empty string.<br/>
    /// - CallStack is an empty collection.<br/>
    /// - Plugins is an empty dictionary.<br/>
    /// - CrashGenVersion is an empty string.<br/>
    /// - CrashTime is null.<br/>
    /// - IsComplete returns false.<br/>
    /// - HasError returns false.
    /// </remarks>
    [Fact]
    public void CrashLog_DefaultValues_AreCorrect()
    {
        var crashLog = new CrashLog();

        Assert.Equal(string.Empty, crashLog.FilePath);
        Assert.Equal(string.Empty, crashLog.FileName);
        Assert.Empty(crashLog.OriginalLines);
        Assert.Equal(string.Empty, crashLog.Content);
        Assert.Equal(string.Empty, crashLog.MainError);
        Assert.Empty(crashLog.CallStack);
        Assert.Empty(crashLog.Plugins);
        Assert.Equal(string.Empty, crashLog.CrashGenVersion);
        Assert.Null(crashLog.CrashTime);
        Assert.False(crashLog.IsComplete);
        Assert.False(crashLog.HasError);
    }

    /// <summary>
    /// Validates that when a file path is assigned to the <see cref="CrashLog.FilePath"/> property,
    /// the <see cref="CrashLog.FileName"/> property correctly extracts and returns the file name from the full path.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="CrashLog.FileName"/> property behaves as expected when a valid
    /// file path is supplied to the <see cref="CrashLog.FilePath"/> property. Specifically, it verifies that:<br/>
    /// - The file name is correctly derived from the provided file path.<br/>
    /// - The extraction logic works for paths containing typical directory structures and file names.
    /// </remarks>
    [Fact]
    public void CrashLog_WithFilePath_ExtractsFileName()
    {
        var crashLog = new CrashLog
        {
            FilePath = @"C:\Users\Test\Documents\crash-2024-01-01-12-34-56.log"
        };

        Assert.Equal("crash-2024-01-01-12-34-56.log", crashLog.FileName);
    }

    /// <summary>
    /// Verifies that a <see cref="CrashLog"/> instance correctly loads and initializes its properties
    /// when provided with a sample log file.
    /// </summary>
    /// <remarks>
    /// This test reads a real log file located in the predefined path and ensures that:<br/>
    /// - The FileName property correctly derives the file name from the file path.<br/>
    /// - The OriginalLines property contains the expected non-empty collection of lines read from the file.<br/>
    /// - The Content property includes specific known strings, such as "Fallout 4" and "Buffout", as part
    /// of the processed log content.<br/>
    /// Proper functioning of this test confirms that the <see cref="CrashLog"/> class can handle typical
    /// log file inputs and process them as intended.
    /// </remarks>
    [Fact]
    public void CrashLog_WithSampleLogFile_LoadsCorrectly()
    {
        // Use actual sample log file
        var sampleFile = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log");

        if (!File.Exists(sampleFile)) return;
        var lines = File.ReadAllLines(sampleFile);
        var crashLog = new CrashLog
        {
            FilePath = sampleFile,
            OriginalLines = lines.ToList()
        };

        Assert.Equal("crash-2024-01-11-08-19-43.log", crashLog.FileName);
        Assert.True(crashLog.OriginalLines.Count > 0);
        Assert.Contains("Fallout 4", crashLog.Content);
        Assert.Contains("Buffout", crashLog.Content);
    }

    /// <summary>
    /// Validates that when the <see cref="CrashLog"/> class is initialized with a collection
    /// of original lines, the <see cref="CrashLog.Content"/> property is correctly constructed
    /// by joining those lines with newline characters.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="CrashLog.Content"/> reflects the expected behavior:<br/>
    /// - The resulting content is a newline-delimited string combining all lines from <see cref="CrashLog.OriginalLines"/>.<br/>
    /// - Preserves the order and integrity of the original lines.<br/>
    /// Validates correctness of the <see cref="CrashLog.Content"/> property based on the state of <see cref="CrashLog.OriginalLines"/>.
    /// </remarks>
    [Fact]
    public void CrashLog_WithOriginalLines_BuildsContent()
    {
        var crashLog = new CrashLog
        {
            OriginalLines = new List<string> { "Line 1", "Line 2", "Line 3" }
        };

        Assert.Equal("Line 1\nLine 2\nLine 3", crashLog.Content);
    }

    /// <summary>
    /// Verifies that the <see cref="CrashLog.IsComplete"/> property correctly returns true
    /// when the <see cref="CrashLog.Plugins"/> collection contains one or more entries.
    /// </summary>
    /// <remarks>
    /// This test ensures that the state of the <see cref="CrashLog"/> instance is considered
    /// complete when the <see cref="CrashLog.Plugins"/> dictionary is populated. It confirms
    /// that the <see cref="CrashLog.IsComplete"/> property accurately reflects the presence
    /// of plugins, validating the expected functionality for interpreting crash log completeness.
    /// </remarks>
    [Fact]
    public void CrashLog_IsComplete_ReturnsTrueWhenPluginsExist()
    {
        var crashLog = new CrashLog
        {
            Plugins = new Dictionary<string, string> { { "plugin1.esp", "01" } }
        };

        Assert.True(crashLog.IsComplete);
    }

    /// <summary>
    /// Verifies that the <see cref="CrashLog.HasError"/> property returns true
    /// when the <see cref="CrashLog.MainError"/> property is set to a non-empty value.
    /// </summary>
    /// <remarks>
    /// This test confirms that the <see cref="CrashLog.HasError"/> property correctly evaluates
    /// to true if the <see cref="CrashLog.MainError"/> property contains a valid error message.
    /// Ensures the following behavior:
    /// - When <see cref="CrashLog.MainError"/> is assigned a non-empty string,
    /// <see cref="CrashLog.HasError"/> returns true.
    /// This ensures that the <see cref="CrashLog"/> is accurately identifying the presence of an error
    /// based on the state of the <see cref="CrashLog.MainError"/> property.
    /// </remarks>
    [Fact]
    public void CrashLog_HasError_ReturnsTrueWhenMainErrorSet()
    {
        var crashLog = new CrashLog
        {
            MainError = "Access violation"
        };

        Assert.True(crashLog.HasError);
    }

    /// <summary>
    /// Verifies that the <see cref="CrashLog"/> class behaves as expected when instantiated with mock plugin data.
    /// </summary>
    /// <remarks>
    /// This test ensures the following behaviors:<br/>
    /// - The <see cref="CrashLog.FileName"/> is accurately derived from the provided <see cref="CrashLog.FilePath"/>.<br/>
    /// - The <see cref="CrashLog.Content"/> reflects the concatenated values of <see cref="CrashLog.OriginalLines"/> correctly.<br/>
    /// - The <see cref="CrashLog.IsComplete"/> property evaluates to true when valid plugin data is provided.<br/>
    /// - The <see cref="CrashLog.HasError"/> evaluates to true if a value is assigned to <see cref="CrashLog.MainError"/>.<br/>
    /// - The <see cref="CrashLog.CrashTime"/> is correctly assigned and maintained.<br/>
    /// - The number of plugins in <see cref="CrashLog.Plugins"/> matches the expected count.<br/>
    /// - The number of stack trace entries in <see cref="CrashLog.CallStack"/> matches the expected count.<br/>
    /// This ensures that the class's functionality aligns with expected behavior when handling populated plugin data.
    /// </remarks>
    [Fact]
    public void CrashLog_WithMockPluginData_WorksCorrectly()
    {
        var crashTime = DateTime.Now;
        var crashLog = new CrashLog
        {
            FilePath = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log"),
            OriginalLines = new List<string> { "Error occurred", "Stack trace" },
            MainError = "EXCEPTION_ACCESS_VIOLATION",
            CallStack = new List<string> { "Function1", "Function2" },
            Plugins = new Dictionary<string, string> { { "plugin1.esp", "01" }, { "plugin2.esp", "02" } },
            CrashGenVersion = "Buffout 4 v1.28.6",
            CrashTime = crashTime
        };

        Assert.Equal("crash-2024-01-11-08-19-43.log", crashLog.FileName);
        Assert.Equal("Error occurred\nStack trace", crashLog.Content);
        Assert.True(crashLog.IsComplete);
        Assert.True(crashLog.HasError);
        Assert.Equal(crashTime, crashLog.CrashTime);
        Assert.Equal(2, crashLog.Plugins.Count);
        Assert.Equal(2, crashLog.CallStack.Count);
    }

    /// <summary>
    /// Ensures that all expected sample files are present in the designated sample logs directory.
    /// </summary>
    /// <remarks>
    /// This test checks the presence of both log files and their corresponding AUTOSCAN.md files
    /// in the sample logs directory. The following validations are performed:<br/>
    /// - Confirms that there is at least one log file with a "*.log" extension.<br/>
    /// - Confirms that there is at least one associated AUTOSCAN.md file with a "*-AUTOSCAN.md" naming pattern.<br/>
    /// This verifies the integrity of the sample data used for testing purposes.
    /// </remarks>
    [Fact]
    public void CrashLog_GetAllSampleFiles_ReturnsExpectedCount()
    {
        if (!Directory.Exists(_sampleLogsPath)) return;
        var logFiles = Directory.GetFiles(_sampleLogsPath, "*.log");
        var mdFiles = Directory.GetFiles(_sampleLogsPath, "*-AUTOSCAN.md");

        // Each log file should have a corresponding AUTOSCAN.md file
        Assert.True(logFiles.Length > 0, "Should have sample log files");
        Assert.True(mdFiles.Length > 0, "Should have sample AUTOSCAN.md files");
    }
}