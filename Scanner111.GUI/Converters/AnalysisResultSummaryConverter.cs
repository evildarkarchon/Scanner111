using System.Globalization;
using Avalonia.Data.Converters;
using Scanner111.Core.Analyzers;

namespace Scanner111.GUI.Converters;

/// <summary>
///     Provides a mechanism to convert analysis results into user-friendly summary text for display.
/// </summary>
public class AnalysisResultSummaryConverter : IValueConverter
{
    public static readonly AnalysisResultSummaryConverter Instance = new();

    /// <summary>
    ///     Converts an analysis result into a user-friendly summary text for display purposes.
    /// </summary>
    /// <param name="value">
    ///     The analysis result object to convert. This parameter is expected to be either an
    ///     <see cref="AnalysisResult" /> type or null.
    /// </param>
    /// <param name="targetType">The type of the binding target property. This parameter is not used in this implementation.</param>
    /// <param name="parameter">
    ///     An optional parameter intended for use in the converter. This parameter is not used in this
    ///     implementation.
    /// </param>
    /// <param name="culture">The culture to be used in the converter. This parameter is not used in this implementation.</param>
    /// <returns>
    ///     A string representing a user-friendly summary of the provided analysis result. If the input value is invalid
    ///     or null, a default message is returned.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AnalysisResult result)
            return "No analysis data available";

        if (!result.HasFindings) return GetNoFindingsSummary(result.AnalyzerName);

        // Generate summary based on analyzer type and findings
        return result switch
        {
            FormIdAnalysisResult formIdResult => GetFormIdSummary(formIdResult),
            PluginAnalysisResult pluginResult => GetPluginSummary(pluginResult),
            SuspectAnalysisResult suspectResult => GetSuspectSummary(suspectResult),
            _ => GetGenericSummary(result)
        };
    }

    /// <summary>
    ///     Converts a user-facing value back to its corresponding analysis result object. This operation is not implemented
    ///     for this converter.
    /// </summary>
    /// <param name="value">
    ///     The user-facing value to be converted back. This parameter is expected to be unused in this
    ///     implementation.
    /// </param>
    /// <param name="targetType">
    ///     The type to which the value is being converted back. This parameter is not used in this
    ///     implementation.
    /// </param>
    /// <param name="parameter">
    ///     An optional parameter intended for use in the converter. This parameter is not used in this
    ///     implementation.
    /// </param>
    /// <param name="culture">The culture to be used in the conversion. This parameter is not used in this implementation.</param>
    /// <returns>Throws a <see cref="NotImplementedException" /> as this method is not implemented.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Generates a user-friendly summary message when no findings are detected by the analyzer.
    /// </summary>
    /// <param name="analyzerName">
    ///     The name of the analyzer that performed the analysis. Used to tailor the no-findings message
    ///     based on the type of analysis.
    /// </param>
    /// <returns>A string message indicating that no issues were found during the analysis.</returns>
    private static string GetNoFindingsSummary(string analyzerName)
    {
        return analyzerName.ToLower() switch
        {
            var name when name.Contains("formid") => "No problematic FormIDs detected",
            var name when name.Contains("plugin") => "No plugin conflicts found",
            var name when name.Contains("suspect") => "No known error patterns detected",
            var name when name.Contains("record") => "No corrupted records found",
            var name when name.Contains("settings") => "No configuration issues detected",
            var name when name.Contains("stack") => "No stack trace issues found",
            _ => "Analysis completed successfully"
        };
    }

    /// <summary>
    ///     Generates a summary string for a given FormIdAnalysisResult, indicating the total number of FormIDs
    ///     and the count of resolved or unresolved FormIDs found in the analysis.
    /// </summary>
    /// <param name="result">
    ///     The FormIdAnalysisResult containing details of the analyzed FormIDs. This parameter must not be
    ///     null.
    /// </param>
    /// <returns>
    ///     A string summarizing the FormID analysis results, including the total number of FormIDs and their resolution
    ///     status.
    /// </returns>
    private static string GetFormIdSummary(FormIdAnalysisResult result)
    {
        var formIdCount = result.FormIds.Count;
        var resolvedCount = result.ResolvedFormIds.Count;

        if (formIdCount == 0)
            return "No FormIDs found in crash log";

        var unresolvedCount = formIdCount - resolvedCount;
        return unresolvedCount > 0
            ? $"Found {formIdCount} FormIDs - {unresolvedCount} unresolved (potential issues)"
            : $"Found {formIdCount} FormIDs - all resolved successfully";
    }

    /// <summary>
    ///     Generates a summary of plugin analysis results, focusing on the total plugins detected
    ///     and the number of potentially problematic plugins, if any.
    /// </summary>
    /// <param name="result">
    ///     The plugin analysis result containing details about detected plugins and suspected problematic
    ///     plugins.
    /// </param>
    /// <returns>
    ///     A string summarizing the plugin analysis, indicating detected plugins and any potential issues. If no plugins
    ///     are detected, a default message is returned.
    /// </returns>
    private static string GetPluginSummary(PluginAnalysisResult result)
    {
        var totalPlugins = result.Plugins.Count;
        var suspectedPlugins = result.SuspectedPlugins.Count;

        if (totalPlugins == 0)
            return "No plugins detected in crash log";

        return suspectedPlugins > 0
            ? $"Found {totalPlugins} plugins - {suspectedPlugins} potentially problematic"
            : $"Found {totalPlugins} plugins - no obvious conflicts";
    }

    /// <summary>
    ///     Generates a summary detailing the findings of a suspect pattern analysis result.
    /// </summary>
    /// <param name="result">
    ///     The <see cref="SuspectAnalysisResult" /> containing the analysis data, including error and stack
    ///     matches.
    /// </param>
    /// <returns>
    ///     A string summarizing the number of error and stack pattern matches found. If no matches are detected, an
    ///     appropriate message is returned.
    /// </returns>
    private static string GetSuspectSummary(SuspectAnalysisResult result)
    {
        var errorMatches = result.ErrorMatches.Count;
        var stackMatches = result.StackMatches.Count;
        var totalMatches = errorMatches + stackMatches;

        if (totalMatches == 0)
            return "No known error patterns detected";

        var parts = new List<string>();
        if (errorMatches > 0) parts.Add($"{errorMatches} error pattern(s)");
        if (stackMatches > 0) parts.Add($"{stackMatches} stack pattern(s)");

        return $"Detected {string.Join(" and ", parts)}";
    }

    /// <summary>
    ///     Generates a generic summary based on the analysis result, including count of findings and a sample report line if
    ///     available.
    /// </summary>
    /// <param name="result">
    ///     An instance of <see cref="AnalysisResult" /> containing analysis information, including findings
    ///     and report lines.
    /// </param>
    /// <returns>
    ///     A string summarizing the analysis. If there are no report lines, a generic message is returned. If report
    ///     lines exist, the first line is included alongside the number of findings.
    /// </returns>
    private static string GetGenericSummary(AnalysisResult result)
    {
        var reportLineCount = result.ReportLines.Count;

        if (reportLineCount == 0)
            return "Analysis completed - see full report for details";

        // Try to extract meaningful info from first report line
        var firstLine = result.ReportLines.FirstOrDefault()?.Trim();
        return !string.IsNullOrEmpty(firstLine)
            ? $"{reportLineCount} finding(s) - {firstLine}"
            : $"Analysis completed with {reportLineCount} finding(s)";
    }
}