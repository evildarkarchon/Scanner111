using System.Data;
using System.Data.SQLite;
using System.Text.Json;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Services;

public class StatisticsService : IStatisticsService, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _dbSemaphore = new(1, 1);
    private bool _isInitialized;

    public StatisticsService(IApplicationSettingsService? settingsService = null)
        : this(GetDefaultDatabasePath())
    {
    }

    public StatisticsService(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _connectionString = $"Data Source={databasePath};Version=3;";

        // Initialize database asynchronously but don't wait
        _ = InitializeDatabaseAsync();
    }

    public void Dispose()
    {
        _dbSemaphore?.Dispose();
    }

    public async Task RecordScanAsync(ScanStatistics statistics)
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var insertCommand = @"
                INSERT INTO ScanStatistics (
                    Timestamp, LogFilePath, GameType, TotalIssuesFound,
                    CriticalIssues, WarningIssues, InfoIssues, ProcessingTimeMs,
                    WasSolved, PrimaryIssueType, ResolvedBy, IssuesByType
                ) VALUES (
                    @timestamp, @logFilePath, @gameType, @totalIssuesFound,
                    @criticalIssues, @warningIssues, @infoIssues, @processingTimeMs,
                    @wasSolved, @primaryIssueType, @resolvedBy, @issuesByType
                )
            ";

            using var command = new SQLiteCommand(insertCommand, connection);
            command.Parameters.AddWithValue("@timestamp", statistics.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("@logFilePath", statistics.LogFilePath);
            command.Parameters.AddWithValue("@gameType", statistics.GameType ?? "");
            command.Parameters.AddWithValue("@totalIssuesFound", statistics.TotalIssuesFound);
            command.Parameters.AddWithValue("@criticalIssues", statistics.CriticalIssues);
            command.Parameters.AddWithValue("@warningIssues", statistics.WarningIssues);
            command.Parameters.AddWithValue("@infoIssues", statistics.InfoIssues);
            command.Parameters.AddWithValue("@processingTimeMs", (long)statistics.ProcessingTime.TotalMilliseconds);
            command.Parameters.AddWithValue("@wasSolved", statistics.WasSolved ? 1 : 0);
            command.Parameters.AddWithValue("@primaryIssueType", statistics.PrimaryIssueType ?? "");
            command.Parameters.AddWithValue("@resolvedBy", statistics.ResolvedBy ?? "");
            command.Parameters.AddWithValue("@issuesByType", JsonSerializer.Serialize(statistics.IssuesByType));

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<ScanStatistics?> GetLatestScanAsync()
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = "SELECT * FROM ScanStatistics ORDER BY Timestamp DESC LIMIT 1";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false)) return ReadStatisticsFromReader(reader);

            return null;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<IEnumerable<ScanStatistics>> GetRecentScansAsync(int count = 10)
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = "SELECT * FROM ScanStatistics ORDER BY Timestamp DESC LIMIT @count";
            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@count", count);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var results = new List<ScanStatistics>();

            while (await reader.ReadAsync().ConfigureAwait(false)) results.Add(ReadStatisticsFromReader(reader));

            return results;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<IEnumerable<ScanStatistics>> GetScansInDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = @"
                SELECT * FROM ScanStatistics 
                WHERE Timestamp >= @startDate AND Timestamp <= @endDate 
                ORDER BY Timestamp DESC
            ";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@startDate", startDate.ToString("O"));
            command.Parameters.AddWithValue("@endDate", endDate.ToString("O"));

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var results = new List<ScanStatistics>();

            while (await reader.ReadAsync().ConfigureAwait(false)) results.Add(ReadStatisticsFromReader(reader));

            return results;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<StatisticsSummary> GetSummaryAsync()
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var summaryQuery = @"
                SELECT 
                    COUNT(*) as TotalScans,
                    SUM(CASE WHEN WasSolved = 1 THEN 1 ELSE 0 END) as SuccessfulScans,
                    SUM(TotalIssuesFound) as TotalIssuesFound,
                    SUM(CriticalIssues) as TotalCriticalIssues,
                    SUM(WarningIssues) as TotalWarningIssues,
                    SUM(InfoIssues) as TotalInfoIssues,
                    SUM(ProcessingTimeMs) as TotalProcessingTimeMs,
                    AVG(ProcessingTimeMs) as AvgProcessingTimeMs,
                    MIN(Timestamp) as FirstScan,
                    MAX(Timestamp) as LastScan
                FROM ScanStatistics
            ";

            using var command = new SQLiteCommand(summaryQuery, connection);
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            var summary = new StatisticsSummary();

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                summary.TotalScans = reader.GetInt32(reader.GetOrdinal("TotalScans"));
                summary.SuccessfulScans = reader.IsDBNull(reader.GetOrdinal("SuccessfulScans"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("SuccessfulScans"));
                summary.FailedScans = summary.TotalScans - summary.SuccessfulScans;
                summary.TotalIssuesFound = reader.IsDBNull(reader.GetOrdinal("TotalIssuesFound"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalIssuesFound"));
                summary.TotalCriticalIssues = reader.IsDBNull(reader.GetOrdinal("TotalCriticalIssues"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalCriticalIssues"));
                summary.TotalWarningIssues = reader.IsDBNull(reader.GetOrdinal("TotalWarningIssues"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalWarningIssues"));
                summary.TotalInfoIssues = reader.IsDBNull(reader.GetOrdinal("TotalInfoIssues"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalInfoIssues"));

                var totalMs = reader.IsDBNull(reader.GetOrdinal("TotalProcessingTimeMs"))
                    ? 0
                    : reader.GetInt64(reader.GetOrdinal("TotalProcessingTimeMs"));
                summary.TotalProcessingTime = TimeSpan.FromMilliseconds(totalMs);

                var avgMs = reader.IsDBNull(reader.GetOrdinal("AvgProcessingTimeMs"))
                    ? 0
                    : reader.GetDouble(reader.GetOrdinal("AvgProcessingTimeMs"));
                summary.AverageProcessingTime = TimeSpan.FromMilliseconds(avgMs);

                summary.SolveRate = summary.TotalScans > 0
                    ? (double)summary.SuccessfulScans / summary.TotalScans * 100
                    : 0;

                if (!reader.IsDBNull(reader.GetOrdinal("FirstScan")))
                    summary.FirstScan = DateTime.Parse(reader.GetString(reader.GetOrdinal("FirstScan")));

                if (!reader.IsDBNull(reader.GetOrdinal("LastScan")))
                    summary.LastScan = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastScan")));
            }

            // Get most common issues
            var issueQuery = @"
                SELECT PrimaryIssueType, COUNT(*) as Count 
                FROM ScanStatistics 
                WHERE PrimaryIssueType IS NOT NULL AND PrimaryIssueType != ''
                GROUP BY PrimaryIssueType 
                ORDER BY Count DESC 
                LIMIT 10
            ";

            using var issueCommand = new SQLiteCommand(issueQuery, connection);
            using var issueReader = await issueCommand.ExecuteReaderAsync().ConfigureAwait(false);

            while (await issueReader.ReadAsync().ConfigureAwait(false))
            {
                var issueType = issueReader.GetString(0);
                var count = issueReader.GetInt32(1);
                summary.MostCommonIssues[issueType] = count;
            }

            return summary;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<StatisticsSummary> GetSummaryForDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var summaryQuery = @"
                SELECT 
                    COUNT(*) as TotalScans,
                    SUM(CASE WHEN WasSolved = 1 THEN 1 ELSE 0 END) as SuccessfulScans,
                    SUM(TotalIssuesFound) as TotalIssuesFound,
                    SUM(CriticalIssues) as TotalCriticalIssues,
                    SUM(WarningIssues) as TotalWarningIssues,
                    SUM(InfoIssues) as TotalInfoIssues,
                    SUM(ProcessingTimeMs) as TotalProcessingTimeMs,
                    AVG(ProcessingTimeMs) as AvgProcessingTimeMs,
                    MIN(Timestamp) as FirstScan,
                    MAX(Timestamp) as LastScan
                FROM ScanStatistics
                WHERE Timestamp >= @startDate AND Timestamp <= @endDate
            ";

            using var command = new SQLiteCommand(summaryQuery, connection);
            command.Parameters.AddWithValue("@startDate", startDate.ToString("O"));
            command.Parameters.AddWithValue("@endDate", endDate.ToString("O"));

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            var summary = new StatisticsSummary();

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                summary.TotalScans = reader.GetInt32(reader.GetOrdinal("TotalScans"));
                summary.SuccessfulScans = reader.IsDBNull(reader.GetOrdinal("SuccessfulScans"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("SuccessfulScans"));
                summary.FailedScans = summary.TotalScans - summary.SuccessfulScans;
                summary.TotalIssuesFound = reader.IsDBNull(reader.GetOrdinal("TotalIssuesFound"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalIssuesFound"));
                summary.TotalCriticalIssues = reader.IsDBNull(reader.GetOrdinal("TotalCriticalIssues"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalCriticalIssues"));
                summary.TotalWarningIssues = reader.IsDBNull(reader.GetOrdinal("TotalWarningIssues"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalWarningIssues"));
                summary.TotalInfoIssues = reader.IsDBNull(reader.GetOrdinal("TotalInfoIssues"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("TotalInfoIssues"));

                var totalMs = reader.IsDBNull(reader.GetOrdinal("TotalProcessingTimeMs"))
                    ? 0
                    : reader.GetInt64(reader.GetOrdinal("TotalProcessingTimeMs"));
                summary.TotalProcessingTime = TimeSpan.FromMilliseconds(totalMs);

                var avgMs = reader.IsDBNull(reader.GetOrdinal("AvgProcessingTimeMs"))
                    ? 0
                    : reader.GetDouble(reader.GetOrdinal("AvgProcessingTimeMs"));
                summary.AverageProcessingTime = TimeSpan.FromMilliseconds(avgMs);

                summary.SolveRate = summary.TotalScans > 0
                    ? (double)summary.SuccessfulScans / summary.TotalScans * 100
                    : 0;

                if (!reader.IsDBNull(reader.GetOrdinal("FirstScan")))
                    summary.FirstScan = DateTime.Parse(reader.GetString(reader.GetOrdinal("FirstScan")));

                if (!reader.IsDBNull(reader.GetOrdinal("LastScan")))
                    summary.LastScan = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastScan")));
            }

            return summary;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task ClearStatisticsAsync()
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var deleteCommand = "DELETE FROM ScanStatistics";
            using var command = new SQLiteCommand(deleteCommand, connection);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<IEnumerable<IssueTypeStatistics>> GetIssueTypeStatisticsAsync()
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = @"
                SELECT 
                    PrimaryIssueType,
                    COUNT(*) as Count,
                    COUNT(*) * 100.0 / (SELECT COUNT(*) FROM ScanStatistics WHERE PrimaryIssueType IS NOT NULL) as Percentage
                FROM ScanStatistics
                WHERE PrimaryIssueType IS NOT NULL AND PrimaryIssueType != ''
                GROUP BY PrimaryIssueType
                ORDER BY Count DESC
            ";

            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            var results = new List<IssueTypeStatistics>();

            while (await reader.ReadAsync().ConfigureAwait(false))
                results.Add(new IssueTypeStatistics
                {
                    IssueType = reader.GetString(0),
                    Count = reader.GetInt32(1),
                    Percentage = reader.GetDouble(2)
                });

            return results;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    public async Task<IEnumerable<DailyStatistics>> GetDailyStatisticsAsync(int days = 30)
    {
        await InitializeDatabaseAsync().ConfigureAwait(false);

        var startDate = DateTime.Now.Date.AddDays(-days);

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = @"
                SELECT 
                    DATE(Timestamp) as Date,
                    COUNT(*) as ScanCount,
                    SUM(TotalIssuesFound) as IssuesFound,
                    SUM(CASE WHEN WasSolved = 1 THEN TotalIssuesFound ELSE 0 END) as IssuesSolved,
                    SUM(ProcessingTimeMs) as TotalProcessingTimeMs
                FROM ScanStatistics
                WHERE Timestamp >= @startDate
                GROUP BY DATE(Timestamp)
                ORDER BY Date DESC
            ";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@startDate", startDate.ToString("O"));

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var results = new List<DailyStatistics>();

            while (await reader.ReadAsync().ConfigureAwait(false))
                results.Add(new DailyStatistics
                {
                    Date = DateTime.Parse(reader.GetString(0)),
                    ScanCount = reader.GetInt32(1),
                    IssuesFound = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    IssuesSolved = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    TotalProcessingTime = TimeSpan.FromMilliseconds(reader.IsDBNull(4) ? 0 : reader.GetInt64(4))
                });

            return results;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private static string GetDefaultDatabasePath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111"
        );

        if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

        return Path.Combine(appDataPath, "statistics.db");
    }

    private async Task InitializeDatabaseAsync()
    {
        if (_isInitialized) return;

        await _dbSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var createTableCommand = @"
                CREATE TABLE IF NOT EXISTS ScanStatistics (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    LogFilePath TEXT NOT NULL,
                    GameType TEXT,
                    TotalIssuesFound INTEGER,
                    CriticalIssues INTEGER,
                    WarningIssues INTEGER,
                    InfoIssues INTEGER,
                    ProcessingTimeMs INTEGER,
                    WasSolved INTEGER,
                    PrimaryIssueType TEXT,
                    ResolvedBy TEXT,
                    IssuesByType TEXT
                );
                
                CREATE INDEX IF NOT EXISTS idx_timestamp ON ScanStatistics(Timestamp);
                CREATE INDEX IF NOT EXISTS idx_game_type ON ScanStatistics(GameType);
                CREATE INDEX IF NOT EXISTS idx_was_solved ON ScanStatistics(WasSolved);
            ";

            using var command = new SQLiteCommand(createTableCommand, connection);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            _isInitialized = true;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private static ScanStatistics ReadStatisticsFromReader(IDataRecord reader)
    {
        var stats = new ScanStatistics
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
            LogFilePath = reader.GetString(reader.GetOrdinal("LogFilePath")),
            GameType = reader.GetString(reader.GetOrdinal("GameType")),
            TotalIssuesFound = reader.GetInt32(reader.GetOrdinal("TotalIssuesFound")),
            CriticalIssues = reader.GetInt32(reader.GetOrdinal("CriticalIssues")),
            WarningIssues = reader.GetInt32(reader.GetOrdinal("WarningIssues")),
            InfoIssues = reader.GetInt32(reader.GetOrdinal("InfoIssues")),
            ProcessingTime = TimeSpan.FromMilliseconds(reader.GetInt64(reader.GetOrdinal("ProcessingTimeMs"))),
            WasSolved = reader.GetInt32(reader.GetOrdinal("WasSolved")) == 1,
            PrimaryIssueType = reader.IsDBNull(reader.GetOrdinal("PrimaryIssueType"))
                ? null
                : reader.GetString(reader.GetOrdinal("PrimaryIssueType")),
            ResolvedBy = reader.IsDBNull(reader.GetOrdinal("ResolvedBy"))
                ? null
                : reader.GetString(reader.GetOrdinal("ResolvedBy"))
        };

        var issuesByTypeJson = reader.GetString(reader.GetOrdinal("IssuesByType"));
        if (!string.IsNullOrEmpty(issuesByTypeJson))
            try
            {
                stats.IssuesByType = JsonSerializer.Deserialize<Dictionary<string, int>>(issuesByTypeJson)
                                     ?? new Dictionary<string, int>();
            }
            catch
            {
                stats.IssuesByType = new Dictionary<string, int>();
            }

        return stats;
    }
}