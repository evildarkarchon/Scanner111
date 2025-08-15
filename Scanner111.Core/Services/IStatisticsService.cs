namespace Scanner111.Core.Services;

public interface IStatisticsService
{
    Task RecordScanAsync(ScanStatistics statistics);
    Task<ScanStatistics?> GetLatestScanAsync();
    Task<IEnumerable<ScanStatistics>> GetRecentScansAsync(int count = 10);
    Task<IEnumerable<ScanStatistics>> GetScansInDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<StatisticsSummary> GetSummaryAsync();
    Task<StatisticsSummary> GetSummaryForDateRangeAsync(DateTime startDate, DateTime endDate);
    Task ClearStatisticsAsync();
    Task<IEnumerable<IssueTypeStatistics>> GetIssueTypeStatisticsAsync();
    Task<IEnumerable<DailyStatistics>> GetDailyStatisticsAsync(int days = 30);
}

public class ScanStatistics
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string LogFilePath { get; set; } = "";
    public string GameType { get; set; } = "";
    public int TotalIssuesFound { get; set; }
    public int CriticalIssues { get; set; }
    public int WarningIssues { get; set; }
    public int InfoIssues { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public bool WasSolved { get; set; }
    public string? PrimaryIssueType { get; set; }
    public string? ResolvedBy { get; set; }
    public Dictionary<string, int> IssuesByType { get; set; } = new();
}

public class StatisticsSummary
{
    public int TotalScans { get; set; }
    public int SuccessfulScans { get; set; }
    public int FailedScans { get; set; }
    public int TotalIssuesFound { get; set; }
    public int TotalCriticalIssues { get; set; }
    public int TotalWarningIssues { get; set; }
    public int TotalInfoIssues { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public double SolveRate { get; set; }
    public DateTime FirstScan { get; set; }
    public DateTime LastScan { get; set; }
    public Dictionary<string, int> MostCommonIssues { get; set; } = new();
}

public class IssueTypeStatistics
{
    public string IssueType { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class DailyStatistics
{
    public DateTime Date { get; set; }
    public int ScanCount { get; set; }
    public int IssuesFound { get; set; }
    public int IssuesSolved { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
}