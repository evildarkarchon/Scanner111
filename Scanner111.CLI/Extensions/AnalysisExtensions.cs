using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Extensions;

/// <summary>
/// Extension methods for analysis-related types.
/// </summary>
public static class AnalysisExtensions
{
    /// <summary>
    /// Gets the title from an AnalysisResult.
    /// </summary>
    public static string GetTitle(this AnalysisResult result)
    {
        if (result?.Fragment?.Title != null)
            return result.Fragment.Title;
        
        return result?.AnalyzerName ?? "Unknown Analyzer";
    }
    
    /// <summary>
    /// Gets the summary from an AnalysisResult's fragment.
    /// </summary>
    public static string GetSummary(this AnalysisResult result)
    {
        if (result?.Fragment?.Content != null)
            return result.Fragment.Content;
        
        if (result?.Fragment != null && result.Fragment.Children.Any())
        {
            // Try to get content from first child
            var firstChild = result.Fragment.Children.FirstOrDefault();
            if (firstChild?.Content != null)
                return firstChild.Content;
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// Gets the summary from a ReportFragment.
    /// </summary>
    public static string GetSummary(this ReportFragment fragment)
    {
        if (fragment?.Content != null)
            return fragment.Content;
        
        if (fragment != null && fragment.Children.Any())
        {
            // Try to get content from first child
            var firstChild = fragment.Children.FirstOrDefault();
            if (firstChild?.Content != null)
                return firstChild.Content;
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// Gets the severity as a display string.
    /// </summary>
    public static string GetSeverityDisplay(this AnalysisSeverity severity)
    {
        return severity switch
        {
            AnalysisSeverity.Critical => "[red]CRITICAL[/]",
            AnalysisSeverity.Error => "[red]ERROR[/]",
            AnalysisSeverity.Warning => "[yellow]WARNING[/]",
            AnalysisSeverity.Info => "[cyan]INFO[/]",
            AnalysisSeverity.None => "[green]OK[/]",
            _ => severity.ToString()
        };
    }
    
    /// <summary>
    /// Gets the severity color for Spectre.Console.
    /// </summary>
    public static string GetSeverityColor(this AnalysisSeverity severity)
    {
        return severity switch
        {
            AnalysisSeverity.Critical => "red",
            AnalysisSeverity.Error => "red",
            AnalysisSeverity.Warning => "yellow",
            AnalysisSeverity.Info => "cyan",
            AnalysisSeverity.None => "green",
            _ => "white"
        };
    }
    
    /// <summary>
    /// Determines severity from a FragmentType.
    /// </summary>
    public static AnalysisSeverity GetSeverityFromFragmentType(this FragmentType fragmentType)
    {
        return fragmentType switch
        {
            FragmentType.Error => AnalysisSeverity.Error,
            FragmentType.Warning => AnalysisSeverity.Warning,
            FragmentType.Info => AnalysisSeverity.Info,
            _ => AnalysisSeverity.None
        };
    }
}