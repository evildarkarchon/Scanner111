using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Models;

/// <summary>
/// Extension methods for ReportFragment to work with CLI severity levels.
/// </summary>
public static class ReportFragmentExtensions
{
    /// <summary>
    /// Gets the severity level of a report fragment based on its type.
    /// </summary>
    /// <param name="fragment">The report fragment.</param>
    /// <returns>The severity level.</returns>
    public static Severity GetSeverity(this ReportFragment fragment)
    {
        if (fragment == null)
            return Severity.Info;
            
        return fragment.Type switch
        {
            FragmentType.Error => Severity.Error,
            FragmentType.Warning => Severity.Warning,
            FragmentType.Info => Severity.Info,
            FragmentType.Container => Severity.Info,
            FragmentType.Conditional => Severity.Info,
            _ => Severity.Info
        };
    }
    
    /// <summary>
    /// Determines if a fragment represents a critical issue.
    /// </summary>
    /// <param name="fragment">The report fragment.</param>
    /// <returns>True if critical; otherwise, false.</returns>
    public static bool IsCritical(this ReportFragment fragment)
    {
        if (fragment == null)
            return false;
            
        // Check if the title or content contains critical keywords
        var title = fragment.Title?.ToLowerInvariant() ?? "";
        var content = fragment.Content?.ToLowerInvariant() ?? "";
        
        return title.Contains("critical") || 
               title.Contains("fatal") || 
               content.Contains("critical") || 
               content.Contains("fatal");
    }
}