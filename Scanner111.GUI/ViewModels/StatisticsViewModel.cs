using Scanner111.Core.Services;

namespace Scanner111.GUI.ViewModels;

public class StatisticsViewModel : ViewModelBase
{
    private readonly IStatisticsService _statisticsService;
    private string _avgProcessingTime = "0ms";
    private int _failedScans;
    private bool _isLoading;
    private ObservableCollection<ScanStatisticsViewModel> _recentScans = new();
    private int _successfulScans;
    private int _totalIssues;
    private int _totalScans;

    public StatisticsViewModel(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;

        // Create a command for loading statistics
        LoadStatisticsCommand = ReactiveCommand.CreateFromTask(LoadStatisticsAsync);

        // Automatically load on creation (can be disabled for testing)
        if (!IsInDesignMode) LoadStatisticsCommand.Execute().Subscribe();
    }

    public ReactiveCommand<Unit, Unit> LoadStatisticsCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private static bool IsInDesignMode => false; // Can be set to true in tests

    public int TotalScans
    {
        get => _totalScans;
        set => this.RaiseAndSetIfChanged(ref _totalScans, value);
    }

    public int SuccessfulScans
    {
        get => _successfulScans;
        set => this.RaiseAndSetIfChanged(ref _successfulScans, value);
    }

    public int FailedScans
    {
        get => _failedScans;
        set => this.RaiseAndSetIfChanged(ref _failedScans, value);
    }

    public int TotalIssues
    {
        get => _totalIssues;
        set => this.RaiseAndSetIfChanged(ref _totalIssues, value);
    }

    public string AvgProcessingTime
    {
        get => _avgProcessingTime;
        set => this.RaiseAndSetIfChanged(ref _avgProcessingTime, value);
    }

    public ObservableCollection<ScanStatisticsViewModel> RecentScans
    {
        get => _recentScans;
        set => this.RaiseAndSetIfChanged(ref _recentScans, value);
    }

    public async Task LoadStatisticsAsync()
    {
        IsLoading = true;
        try
        {
            var summary = await _statisticsService.GetSummaryAsync();
            TotalScans = summary.TotalScans;
            SuccessfulScans = summary.SuccessfulScans;
            FailedScans = summary.FailedScans;
            TotalIssues = summary.TotalIssuesFound;
            AvgProcessingTime = $"{summary.AverageProcessingTime.TotalMilliseconds:F0}ms";

            var recentScans = await _statisticsService.GetRecentScansAsync();
            RecentScans = new ObservableCollection<ScanStatisticsViewModel>(
                recentScans.Select(s => new ScanStatisticsViewModel(s))
            );
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error loading statistics: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class ScanStatisticsViewModel
{
    private readonly ScanStatistics _statistics;

    public ScanStatisticsViewModel(ScanStatistics statistics)
    {
        _statistics = statistics;
    }

    public string LogFilePath => Path.GetFileName(_statistics.LogFilePath);
    public string Timestamp => _statistics.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    public string IssuesFoundText =>
        $"{_statistics.TotalIssuesFound} issues found ({_statistics.CriticalIssues} critical)";
}