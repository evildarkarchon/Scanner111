using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
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

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectedCommand { get; }

    public ResultsViewModel(IScanResultsService scanResultsService)
    {
        _scanResultsService = scanResultsService;

        // Refresh command - selects first result if available
        RefreshCommand = ReactiveCommand.Create(() =>
        {
            if (Results.Count > 0)
            {
                SelectedResult = Results[0];
            }
        });

        // Delete selected command - only enabled when something is selected
        var canDelete = this.WhenAnyValue(x => x.SelectedResult).Select(result => result is not null);
        DeleteSelectedCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedResult != null)
            {
                var index = Results.IndexOf(SelectedResult);
                Results.Remove(SelectedResult);

                // Select next item or previous if at end
                if (Results.Count > 0)
                {
                    SelectedResult = Results[Math.Min(index, Results.Count - 1)];
                }
                else
                {
                    SelectedResult = null;
                }
            }
        }, canDelete);

        // Auto-select first result when collection changes
        Results.CollectionChanged += OnResultsCollectionChanged;

        // Select the first result if available
        if (Results.Count > 0)
        {
            SelectedResult = Results[0];
        }
    }

    private void OnResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && SelectedResult == null && Results.Count > 0)
        {
            SelectedResult = Results[0];
        }
    }
}
