using FluentAssertions;
using Scanner111.Core.Models;

namespace Scanner111.Tests.Models;

/// <summary>
/// Unit test class for the <see cref="ScanResult"/> and <see cref="ScanStatistics"/> models.
/// </summary>
/// <remarks>
/// This test class is designed to validate the functionality of the <see cref="ScanResult"/> and
/// <see cref="ScanStatistics"/> classes, ensuring their properties and behavior are correct
/// across multiple scenarios.
/// </remarks>
public class ScanResultTests
{
    private readonly string _sampleLogsPath = Path.Combine(
        Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
        "sample_logs"
    );

    /// <summary>
    /// Unit test to verify that the default values of the <see cref="ScanResult"/> class are correctly initialized.
    /// </summary>
    /// <remarks>
    /// This test ensures that the properties of a newly created <see cref="ScanResult"/> object are initialized with the expected default values:<br/>
    /// - <c>LogPath</c> matches the provided input.<br/>
    /// - <c>Report</c> is an empty list.<br/>
    /// - <c>Failed</c> is <c>false</c>.<br/>
    /// - <c>Statistics</c> is not <c>null</c>.<br/>
    /// - <c>ReportText</c> is an empty string if no report is present.<br/>
    /// - <c>OutputPath</c> is generated correctly based on <c>LogPath</c>.
    /// </remarks>
    [Fact]
    public void ScanResult_DefaultValues_AreCorrect()
    {
        var result = new ScanResult { LogPath = "test.log" };

        result.LogPath.Should().Be("test.log", "because LogPath should match the input value");
        result.Report.Should().BeEmpty("because no report was provided");
        result.Failed.Should().BeFalse("because default status is not failed");
        result.Statistics.Should().NotBeNull("because Statistics should be initialized");
        result.ReportText.Should().Be(string.Empty, "because no report content exists");
        result.OutputPath.Should().Be("test-AUTOSCAN.md", "because OutputPath appends -AUTOSCAN.md to the base name");
    }

    /// <summary>
    /// Unit test to validate that the <see cref="ScanResult.ReportText"/> property is correctly constructed when a report is provided.
    /// </summary>
    /// <remarks>
    /// This test verifies that when <see cref="ScanResult.Report"/> contains multiple strings,
    /// the <see cref="ScanResult.ReportText"/> property concatenates them correctly into a single formatted string.
    /// </remarks>
    [Fact]
    public void ScanResult_WithReport_BuildsReportText()
    {
        var result = new ScanResult
        {
            LogPath = "test.log",
            Report = new List<string> { "Line 1\n", "Line 2\n", "Line 3\n" }
        };

        result.ReportText.Should().Be("Line 1\nLine 2\nLine 3\n", "because ReportText concatenates all report lines");
    }

    /// <summary>
    /// Unit test to validate that the <see cref="ScanResult.OutputPath"/> property generates the correct output path
    /// when initialized with a sample log file path.
    /// </summary>
    /// <remarks>
    /// This test verifies the behavior of the <see cref="ScanResult"/> class when the <c>LogPath</c> property is
    /// set to a specific sample log file path. It ensures that the resulting <c>OutputPath</c> is correctly
    /// derived from the <c>LogPath</c> by appending the "-AUTOSCAN.md" suffix while removing the original file extension.
    /// </remarks>
    [Fact]
    public void ScanResult_WithSampleLogPath_GeneratesCorrectOutputPath()
    {
        var sampleFile = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log");
        var result = new ScanResult { LogPath = sampleFile };

        var expectedOutput = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43-AUTOSCAN.md");
        result.OutputPath.Should().Be(expectedOutput, "because OutputPath should append -AUTOSCAN.md to the log file base name");
    }

    /// <summary>
    /// Unit test to confirm that the default values of the <see cref="ScanStatistics"/> class are properly initialized.
    /// </summary>
    /// <remarks>
    /// This test ensures the following defaults are correctly set when a new <see cref="ScanStatistics"/> object is created:<br/>
    /// - The dictionary entry <c>"scanned"</c> has a value of <c>0</c>.<br/>
    /// - The dictionary entry <c>"incomplete"</c> has a value of <c>0</c>.<br/>
    /// - The dictionary entry <c>"failed"</c> has a value of <c>0</c>.<br/>
    /// - The <see cref="ScanStatistics.Scanned"/> property equals <c>0</c>.<br/>
    /// - The <see cref="ScanStatistics.Incomplete"/> property equals <c>0</c>.<br/>
    /// - The <see cref="ScanStatistics.Failed"/> property equals <c>0</c>.
    /// </remarks>
    [Fact]
    public void ScanStatistics_DefaultValues_AreCorrect()
    {
        var stats = new ScanStatistics();

        stats["scanned"].Should().Be(0, "because 'scanned' counter starts at 0");
        stats["incomplete"].Should().Be(0, "because 'incomplete' counter starts at 0");
        stats["failed"].Should().Be(0, "because 'failed' counter starts at 0");
        stats.Scanned.Should().Be(0, "because Scanned property starts at 0");
        stats.Incomplete.Should().Be(0, "because Incomplete property starts at 0");
        stats.Failed.Should().Be(0, "because Failed property starts at 0");
    }

    /// <summary>
    /// Unit test to verify that the property accessors of the <see cref="ScanStatistics"/> class function as expected.
    /// </summary>
    /// <remarks>
    /// This test ensures that the getter and setter operations for the properties of the <see cref="ScanStatistics"/> class work correctly:<br/>
    /// - <c>Scanned</c> reflects and updates the value of the "scanned" key in the dictionary.<br/>
    /// - <c>Incomplete</c> reflects and updates the value of the "incomplete" key in the dictionary.<br/>
    /// - <c>Failed</c> reflects and updates the value of the "failed" key in the dictionary.<br/>
    /// Additionally, this test validates that accessing these properties provides consistent results.
    /// </remarks>
    [Fact]
    public void ScanStatistics_Properties_WorkCorrectly()
    {
        var stats = new ScanStatistics
        {
            Scanned = 5,
            Incomplete = 2,
            Failed = 1
        };

        stats["scanned"].Should().Be(5, "because Scanned property was set to 5");
        stats["incomplete"].Should().Be(2, "because Incomplete property was set to 2");
        stats["failed"].Should().Be(1, "because Failed property was set to 1");
    }

    /// <summary>
    /// Unit test to validate the correct functionality of the <see cref="ScanStatistics.Increment"/> method.
    /// </summary>
    /// <remarks>
    /// This test ensures that calling the <see cref="ScanStatistics.Increment"/> method updates the appropriate counters
    /// within the <see cref="ScanStatistics"/> dictionary. Specifically, it checks the following:<br/>
    /// - Incrementing an existing key increments its value appropriately.<br/>
    /// - Incrementing a non-existing key initializes the key and sets its value to 1.<br/>
    /// - The behavior is consistent with multiple keys and values.<br/>
    /// - Keys that are not incremented retain their initial values.
    /// </remarks>
    [Fact]
    public void ScanStatistics_Increment_WorksCorrectly()
    {
        var stats = new ScanStatistics();

        stats.Increment("scanned");
        stats.Increment("scanned");
        stats.Increment("failed");
        stats.Increment("custom");

        stats["scanned"].Should().Be(2, "because 'scanned' was incremented twice");
        stats["failed"].Should().Be(1, "because 'failed' was incremented once");
        stats["custom"].Should().Be(1, "because 'custom' was incremented once");
        stats["incomplete"].Should().Be(0, "because 'incomplete' was not incremented");
    }

    /// <summary>
    /// Unit test to ensure that all properties of the <see cref="ScanResult"/> class function correctly when initialized with values.
    /// </summary>
    /// <remarks>
    /// This test verifies the following behaviors:<br/>
    /// - <c>LogPath</c> is assigned correctly based on a sample log path.<br/>
    /// - <c>Report</c> content is set and accurately constructs <c>ReportText</c>.<br/>
    /// - <c>Status</c> correctly determines the <c>Failed</c> property value.<br/>
    /// - The <c>Statistics</c> object accurately tracks the number of scanned and failed items.<br/>
    /// - <c>OutputPath</c> is generated correctly based on the provided <c>LogPath</c>.
    /// </remarks>
    [Fact]
    public void ScanResult_WithAllProperties_WorksCorrectly()
    {
        var stats = new ScanStatistics();
        stats.Increment("scanned");
        stats.Increment("failed");

        var result = new ScanResult
        {
            LogPath = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log"),
            Report = new List<string> { "Error analysis:\n", "- Issue found\n" },
            Statistics = stats
        };
        result.Status = ScanStatus.Failed;

        result.Failed.Should().BeTrue("because Status was set to Failed");
        result.ReportText.Should().Be("Error analysis:\n- Issue found\n", "because ReportText concatenates report lines");
        result.Statistics.Scanned.Should().Be(1, "because 'scanned' was incremented once");
        result.Statistics.Failed.Should().Be(1, "because 'failed' was incremented once");
        result.OutputPath.Should().Contain("crash-2024-01-11-08-19-43-AUTOSCAN.md", "because OutputPath is derived from LogPath");
    }
}