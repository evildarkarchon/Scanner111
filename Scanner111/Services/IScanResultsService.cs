using Scanner111.Models;
using System.Collections.ObjectModel;

namespace Scanner111.Services;

/// <summary>
/// Service for sharing scan results between ViewModels.
/// </summary>
public interface IScanResultsService
{
    /// <summary>
    /// Gets the collection of scan results.
    /// </summary>
    ObservableCollection<LogAnalysisResultDisplay> Results { get; }

    /// <summary>
    /// Clears all results.
    /// </summary>
    void Clear();
}

/// <summary>
/// Implementation of IScanResultsService.
/// </summary>
public class ScanResultsService : IScanResultsService
{
    public ObservableCollection<LogAnalysisResultDisplay> Results { get; } = new();

    public void Clear()
    {
        Results.Clear();
    }
}
