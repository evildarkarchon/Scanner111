using Avalonia.Data.Converters;
using Scanner111.Core.Analyzers;
using System;
using System.Globalization;
using System.Linq;

namespace Scanner111.GUI.Converters;

/// <summary>
/// Converts AnalysisResult to user-friendly summary text
/// </summary>
public class AnalysisResultSummaryConverter : IValueConverter
{
    public static readonly AnalysisResultSummaryConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AnalysisResult result)
            return "No analysis data available";

        if (!result.HasFindings)
        {
            return GetNoFindingsSummary(result.AnalyzerName);
        }

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
        var formIdCount = result.FormIds?.Count ?? 0;
        var resolvedCount = result.ResolvedFormIds?.Count ?? 0;
        
        if (formIdCount == 0)
            return "No FormIDs found in crash log";
            
        var unresolvedCount = formIdCount - resolvedCount;
        if (unresolvedCount > 0)
            return $"Found {formIdCount} FormIDs - {unresolvedCount} unresolved (potential issues)";
        else
            return $"Found {formIdCount} FormIDs - all resolved successfully";
    }

    private static string GetPluginSummary(PluginAnalysisResult result)
    {
        var totalPlugins = result.Plugins?.Count ?? 0;
        var suspectedPlugins = result.SuspectedPlugins?.Count ?? 0;
        
        if (totalPlugins == 0)
            return "No plugins detected in crash log";
            
        if (suspectedPlugins > 0)
            return $"Found {totalPlugins} plugins - {suspectedPlugins} potentially problematic";
        else
            return $"Found {totalPlugins} plugins - no obvious conflicts";
    }

    private static string GetSuspectSummary(SuspectAnalysisResult result)
    {
        var errorMatches = result.ErrorMatches?.Count ?? 0;
        var stackMatches = result.StackMatches?.Count ?? 0;
        var totalMatches = errorMatches + stackMatches;
        
        if (totalMatches == 0)
            return "No known error patterns detected";
            
        var parts = new System.Collections.Generic.List<string>();
        if (errorMatches > 0) parts.Add($"{errorMatches} error pattern(s)");
        if (stackMatches > 0) parts.Add($"{stackMatches} stack pattern(s)");
        
        return $"Detected {string.Join(" and ", parts)}";
    }

    private static string GetGenericSummary(AnalysisResult result)
    {
        var reportLineCount = result.ReportLines?.Count ?? 0;
        
        if (reportLineCount == 0)
            return "Analysis completed - see full report for details";
            
        // Try to extract meaningful info from first report line
        var firstLine = result.ReportLines?.FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(firstLine))
        {
            var cleaned = firstLine.Replace("- ", "").Replace("* ", "").Trim();
            return cleaned.Length > 80 ? cleaned.Substring(0, 77) + "..." : cleaned;
        }
        
        return $"Generated {reportLineCount} report lines";
    }
}