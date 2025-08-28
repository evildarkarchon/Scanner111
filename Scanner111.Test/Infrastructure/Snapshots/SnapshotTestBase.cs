using System.Runtime.CompilerServices;
using System.Text;
using VerifyXunit;
using VerifyTests;
using Scanner111.Core.Reporting;
using Scanner111.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Scanner111.Test.Infrastructure.Snapshots;

/// <summary>
///     Base class for tests using Verify.Xunit snapshot testing.
///     Provides automatic snapshot verification and management.
/// </summary>
public abstract class SnapshotTestBase : IntegrationTestBase
{
    static SnapshotTestBase()
    {
        // Configure Verify settings
        // Store snapshots in a dedicated directory

        // Configure JSON serialization settings
        VerifierSettings.AddExtraSettings(settings =>
        {
            settings.DefaultValueHandling = Argon.DefaultValueHandling.Include;
            settings.NullValueHandling = Argon.NullValueHandling.Include;
            settings.Formatting = Argon.Formatting.Indented;
        });

        // Custom converters can be added here if needed
        // VerifierSettings.RegisterFileConverter<ReportFragment>(ConvertReportFragment);
        
        // Ignore dynamic/non-deterministic properties
        VerifierSettings.IgnoreMember<ReportFragment>(x => x.CreatedAt);
        VerifierSettings.IgnoreMember<ReportFragment>(x => x.Id);
        // Ignore for AnalysisReport if the type exists
        // VerifierSettings.IgnoreMember<AnalysisReport>(x => x.GeneratedAt);
        // VerifierSettings.IgnoreMember<AnalysisReport>(x => x.AnalysisDuration);
        
        // Scrub sensitive data
        VerifierSettings.AddScrubber(ScrubSensitiveData);
    }

    /// <summary>
    ///     Verifies an object against its snapshot with automatic test method detection.
    /// </summary>
    protected Task VerifyAsync(object target, [CallerMemberName] string testName = "")
    {
        return Verifier.Verify(target, sourceFile: GetSourceFile())
            .UseMethodName(testName)
            .DisableDiff(); // Disable diff tool in CI environments
    }

    /// <summary>
    ///     Verifies a string against its snapshot.
    /// </summary>
    protected Task VerifyStringAsync(string target, string extension = "txt", 
        [CallerMemberName] string testName = "")
    {
        return Verifier.Verify(target, extension: extension, sourceFile: GetSourceFile())
            .UseMethodName(testName);
    }

    /// <summary>
    ///     Verifies a report fragment against its snapshot.
    /// </summary>
    protected Task VerifyReportFragmentAsync(ReportFragment fragment, 
        [CallerMemberName] string testName = "")
    {
        // Simplify the fragment for snapshot testing
        var simplified = new
        {
            fragment.Title,
            fragment.Content,
            fragment.Order,
            fragment.Type,
            fragment.Visibility,
            ChildrenCount = fragment.Children?.Count ?? 0
        };
        return VerifyAsync(simplified, testName);
    }

    /// <summary>
    ///     Verifies JSON content against its snapshot.
    /// </summary>
    protected Task VerifyJsonAsync(string json, [CallerMemberName] string testName = "")
    {
        return VerifyStringAsync(json, "json", testName);
    }

    /// <summary>
    ///     Verifies multiple objects as a collection snapshot.
    /// </summary>
    protected Task VerifyCollectionAsync<T>(IEnumerable<T> items, 
        [CallerMemberName] string testName = "")
    {
        return VerifyAsync(items.ToList(), testName);
    }

    /// <summary>
    ///     Creates parameterized snapshot settings for theory tests.
    /// </summary>
    protected VerifySettings CreateParameterizedSettings(params object[] parameters)
    {
        var settings = new VerifySettings();
        settings.UseParameters(parameters);
        return settings;
    }

    /// <summary>
    ///     Verifies with custom settings.
    /// </summary>
    protected Task VerifyWithSettingsAsync(object target, VerifySettings settings,
        [CallerMemberName] string testName = "")
    {
        return Verifier.Verify(target, settings, sourceFile: GetSourceFile())
            .UseMethodName(testName);
    }

    // Helper methods for snapshot conversion

    // Removed custom converters due to API incompatibility
    // These can be re-added once the proper Verify.Xunit API is determined


    private static void ScrubSensitiveData(StringBuilder sb)
    {
        // Scrub file paths to be platform-agnostic
        sb.Replace(@"C:\Users\", "{UserDir}\\");
        sb.Replace(@"C:/Users/", "{UserDir}/");
        sb.Replace(@"/home/", "{UserDir}/");
        
        // Scrub timestamps
        sb.Replace(System.Text.RegularExpressions.Regex.Matches(sb.ToString(), 
            @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}").ToString(), "{Timestamp}");
        
        // Scrub GUIDs
        sb.Replace(System.Text.RegularExpressions.Regex.Matches(sb.ToString(),
            @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}").ToString(), "{GUID}");
    }

    private string GetSourceFile([CallerFilePath] string sourceFile = "")
    {
        return sourceFile;
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Register snapshot verification services if needed
        services.AddSingleton<ISnapshotComparer, DefaultSnapshotComparer>();
    }
}

/// <summary>
///     Interface for custom snapshot comparison logic.
/// </summary>
public interface ISnapshotComparer
{
    bool AreEquivalent(string expected, string actual);
    string GetDifference(string expected, string actual);
}

/// <summary>
///     Default implementation of snapshot comparison.
/// </summary>
public class DefaultSnapshotComparer : ISnapshotComparer
{
    public bool AreEquivalent(string expected, string actual)
    {
        return string.Equals(expected, actual, StringComparison.Ordinal);
    }

    public string GetDifference(string expected, string actual)
    {
        // Simple line-by-line diff
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var differences = new List<string>();

        var maxLines = Math.Max(expectedLines.Length, actualLines.Length);
        for (int i = 0; i < maxLines; i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<missing>";
            
            if (expectedLine != actualLine)
            {
                differences.Add($"Line {i + 1}:");
                differences.Add($"  Expected: {expectedLine}");
                differences.Add($"  Actual:   {actualLine}");
            }
        }

        return string.Join("\n", differences);
    }
}