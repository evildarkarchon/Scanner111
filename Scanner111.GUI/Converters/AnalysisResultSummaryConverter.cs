using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Scanner111.Core.Analyzers;

namespace Scanner111.GUI.Converters;

/// <summary>
///     Converts AnalysisResult to user-friendly summary text
/// </summary>
public class AnalysisResultSummaryConverter : IValueConverter
{
    public static readonly AnalysisResultSummaryConverter Instance = new();

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

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

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

    private static string GetGenericSummary(AnalysisResult result)
    {
        var reportLineCount = result.ReportLines.Count;

        if (reportLineCount == 0)
            return "Analysis completed - see full report for details";

        // Try to extract meaningful info from first report line
        var firstLine = result.ReportLines.FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(firstLine))
            return $"{reportLineCount} finding(s) - {firstLine}";

        return $"Analysis completed with {reportLineCount} finding(s)";
    }
}