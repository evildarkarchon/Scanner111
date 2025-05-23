using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using Scanner111.Services.Interfaces;

namespace Scanner111.ViewModels;

/// <summary>
///     ViewModel for displaying Papyrus log analysis.
/// </summary>
public class PapyrusLogViewModel : ViewModelBase
{
    private int _dumpCount;
    private bool _isAnalyzing;
    private string _logAnalysisResult = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PapyrusLogViewModel" /> class.
    /// </summary>
    /// <param name="papyrusLogService">The Papyrus log service.</param>
    public PapyrusLogViewModel(IPapyrusLogService papyrusLogService)
    {
        ArgumentNullException.ThrowIfNull(papyrusLogService);

        AnalyzeLogCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            IsAnalyzing = true;

            try
            {
                // Use Task.Run to perform the analysis on a background thread
                var (message, dumpCount) =
                    await Task.Run(papyrusLogService.AnalyzePapyrusLog);

                // Update UI properties on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogAnalysisResult = message;
                    DumpCount = dumpCount;
                });
            }
            finally
            {
                IsAnalyzing = false;
            }
        });

        // Automatically analyze the log when the ViewModel is created
        AnalyzeLogCommand.Execute(null);
    }

    /// <summary>
    ///     Gets or sets the Papyrus log analysis result text.
    /// </summary>
    public string LogAnalysisResult
    {
        get => _logAnalysisResult;
        set => this.RaiseAndSetIfChanged(ref _logAnalysisResult, value);
    }

    /// <summary>
    ///     Gets or sets the number of dumps found in the log.
    /// </summary>
    public int DumpCount
    {
        get => _dumpCount;
        set => this.RaiseAndSetIfChanged(ref _dumpCount, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the log is currently being analyzed.
    /// </summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => this.RaiseAndSetIfChanged(ref _isAnalyzing, value);
    }

    /// <summary>
    ///     Gets the command to analyze the Papyrus log.
    /// </summary>
    public ICommand AnalyzeLogCommand { get; }
}