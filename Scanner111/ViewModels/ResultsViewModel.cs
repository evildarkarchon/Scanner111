using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public class ResultsViewModel : ViewModelBase
{
    private readonly IScanResultsService _scanResultsService;

    /// <summary>
    /// Collection of scan results (backed by shared service).
    /// </summary>
    public ObservableCollection<LogAnalysisResultDisplay> Results => _scanResultsService.Results;

    [Reactive] public LogAnalysisResultDisplay? SelectedResult { get; set; }

    public ResultsViewModel(IScanResultsService scanResultsService)
    {
        _scanResultsService = scanResultsService;

        // Select the first result if available
        if (Results.Count > 0)
        {
            SelectedResult = Results[0];
        }
    }
}
