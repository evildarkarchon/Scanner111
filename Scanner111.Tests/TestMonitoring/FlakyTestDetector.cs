using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Scanner111.Tests.TestMonitoring;

public class FlakyTestDetector
{
    private readonly string _testResultsPath;
    private readonly List<TestRunHistory> _history = new();

    public FlakyTestDetector(string testResultsPath = "TestResults")
    {
        _testResultsPath = testResultsPath;
    }

    public async Task<FlakyTestAnalysis> AnalyzeTestRunsAsync(int numberOfRuns = 10)
    {
        await LoadHistoricalDataAsync();
        
        var analysis = new FlakyTestAnalysis
        {
            AnalyzedAt = DateTime.UtcNow,
            TotalRunsAnalyzed = _history.Count
        };

        if (_history.Count < 2)
        {
            analysis.InsufficientData = true;
            return analysis;
        }

        var testResults = AggregateTestResults();
        
        foreach (var test in testResults)
        {
            var flakinessInfo = AnalyzeTestFlakiness(test.Value);
            if (flakinessInfo != null)
            {
                analysis.FlakyTests.Add(flakinessInfo);
            }

            var inconsistencyInfo = AnalyzeExecutionTimeInconsistency(test.Value);
            if (inconsistencyInfo != null)
            {
                analysis.TimeInconsistentTests.Add(inconsistencyInfo);
            }
        }

        analysis.EnvironmentFactors = DetectEnvironmentFactors(testResults);
        analysis.RecommendedActions = GenerateRecommendations(analysis);

        return analysis;
    }

    private Dictionary<string, List<TestExecutionRecord>> AggregateTestResults()
    {
        var aggregated = new Dictionary<string, List<TestExecutionRecord>>();

        foreach (var run in _history)
        {
            foreach (var test in run.TestResults)
            {
                if (!aggregated.ContainsKey(test.TestName))
                {
                    aggregated[test.TestName] = new List<TestExecutionRecord>();
                }
                aggregated[test.TestName].Add(test);
            }
        }

        return aggregated;
    }

    private FlakyTestDetails? AnalyzeTestFlakiness(List<TestExecutionRecord> records)
    {
        if (records.Count < 3) return null;

        var passCount = records.Count(r => r.Success);
        var failCount = records.Count - passCount;

        if (passCount == 0 || failCount == 0) return null;

        var successRate = (double)passCount / records.Count;
        
        var patterns = DetectFailurePatterns(records);
        var correlations = DetectCorrelations(records);

        return new FlakyTestDetails
        {
            TestName = records.First().TestName,
            SuccessRate = successRate,
            TotalRuns = records.Count,
            FailureCount = failCount,
            FailurePatterns = patterns,
            EnvironmentCorrelations = correlations,
            LastFailure = records.Where(r => !r.Success).OrderByDescending(r => r.ExecutedAt).FirstOrDefault(),
            FlakinessScore = CalculateFlakinessScore(records),
            RecommendedAction = DetermineRecommendedAction(successRate, patterns, correlations)
        };
    }

    private TimeInconsistencyInfo? AnalyzeExecutionTimeInconsistency(List<TestExecutionRecord> records)
    {
        if (records.Count < 5) return null;

        var times = records.Select(r => r.ExecutionTime.TotalMilliseconds).ToList();
        var mean = times.Average();
        var stdDev = Math.Sqrt(times.Sum(t => Math.Pow(t - mean, 2)) / times.Count);
        var coefficientOfVariation = mean > 0 ? stdDev / mean : 0;

        if (coefficientOfVariation < 0.5) return null;

        return new TimeInconsistencyInfo
        {
            TestName = records.First().TestName,
            MeanExecutionTime = TimeSpan.FromMilliseconds(mean),
            StandardDeviation = TimeSpan.FromMilliseconds(stdDev),
            CoefficientOfVariation = coefficientOfVariation,
            MinTime = TimeSpan.FromMilliseconds(times.Min()),
            MaxTime = TimeSpan.FromMilliseconds(times.Max()),
            Outliers = DetectTimeOutliers(times, mean, stdDev)
        };
    }

    private List<string> DetectFailurePatterns(List<TestExecutionRecord> records)
    {
        var patterns = new List<string>();
        
        var consecutiveFailures = 0;
        var maxConsecutiveFailures = 0;
        
        foreach (var record in records.OrderBy(r => r.ExecutedAt))
        {
            if (!record.Success)
            {
                consecutiveFailures++;
                maxConsecutiveFailures = Math.Max(maxConsecutiveFailures, consecutiveFailures);
            }
            else
            {
                consecutiveFailures = 0;
            }
        }

        if (maxConsecutiveFailures >= 3)
        {
            patterns.Add($"Consecutive failures detected (max: {maxConsecutiveFailures})");
        }

        var timeOfDayFailures = records
            .Where(r => !r.Success)
            .GroupBy(r => r.ExecutedAt.Hour / 6)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (timeOfDayFailures != null && timeOfDayFailures.Count() > records.Count(r => !r.Success) * 0.5)
        {
            var timeRange = timeOfDayFailures.Key switch
            {
                0 => "Night (00:00-06:00)",
                1 => "Morning (06:00-12:00)",
                2 => "Afternoon (12:00-18:00)",
                _ => "Evening (18:00-00:00)"
            };
            patterns.Add($"Failures concentrated during {timeRange}");
        }

        return patterns;
    }

    private List<string> DetectCorrelations(List<TestExecutionRecord> records)
    {
        var correlations = new List<string>();

        var failureMessages = records
            .Where(r => !r.Success && !string.IsNullOrEmpty(r.FailureMessage))
            .Select(r => r.FailureMessage!)
            .ToList();

        if (failureMessages.Any(m => m.Contains("timeout", StringComparison.OrdinalIgnoreCase)))
        {
            correlations.Add("Timeout-related failures detected");
        }

        if (failureMessages.Any(m => m.Contains("access", StringComparison.OrdinalIgnoreCase) ||
                                    m.Contains("permission", StringComparison.OrdinalIgnoreCase)))
        {
            correlations.Add("Permission/Access issues detected");
        }

        if (failureMessages.Any(m => m.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                                    m.Contains("network", StringComparison.OrdinalIgnoreCase)))
        {
            correlations.Add("Network-related issues detected");
        }

        return correlations;
    }

    private double CalculateFlakinessScore(List<TestExecutionRecord> records)
    {
        var successRate = (double)records.Count(r => r.Success) / records.Count;
        var intermittencyScore = 1 - Math.Abs(0.5 - successRate) * 2;
        
        var transitions = 0;
        for (int i = 1; i < records.Count; i++)
        {
            if (records[i].Success != records[i - 1].Success)
            {
                transitions++;
            }
        }
        var transitionRate = (double)transitions / (records.Count - 1);
        
        return (intermittencyScore * 0.6) + (transitionRate * 0.4);
    }

    private string DetermineRecommendedAction(double successRate, List<string> patterns, List<string> correlations)
    {
        if (correlations.Contains("Timeout-related failures detected"))
        {
            return "Increase test timeout or optimize test performance";
        }

        if (correlations.Contains("Permission/Access issues detected"))
        {
            return "Review file system access patterns and implement proper mocking";
        }

        if (correlations.Contains("Network-related issues detected"))
        {
            return "Mock external dependencies and network calls";
        }

        if (patterns.Any(p => p.Contains("Consecutive failures")))
        {
            return "Investigate environmental changes or resource contention";
        }

        if (successRate < 0.5)
        {
            return "Test is consistently failing - review test logic and dependencies";
        }

        return "Add retry logic or investigate race conditions";
    }

    private List<TimeOutlier> DetectTimeOutliers(List<double> times, double mean, double stdDev)
    {
        var outliers = new List<TimeOutlier>();
        var threshold = mean + (2 * stdDev);

        for (int i = 0; i < times.Count; i++)
        {
            if (times[i] > threshold)
            {
                outliers.Add(new TimeOutlier
                {
                    Index = i,
                    Value = TimeSpan.FromMilliseconds(times[i]),
                    DeviationFromMean = (times[i] - mean) / stdDev
                });
            }
        }

        return outliers;
    }

    private List<string> DetectEnvironmentFactors(Dictionary<string, List<TestExecutionRecord>> testResults)
    {
        var factors = new List<string>();

        var totalTests = testResults.Count;
        var pathRelatedFailures = testResults.Values
            .SelectMany(v => v)
            .Count(r => !r.Success && 
                       (r.FailureMessage?.Contains("path", StringComparison.OrdinalIgnoreCase) == true ||
                        r.FailureMessage?.Contains("directory", StringComparison.OrdinalIgnoreCase) == true));

        if (pathRelatedFailures > totalTests * 0.1)
        {
            factors.Add($"High rate of path-related failures ({pathRelatedFailures} occurrences)");
        }

        var concurrentFailures = testResults.Values
            .SelectMany(v => v)
            .Where(r => !r.Success)
            .GroupBy(r => r.ExecutedAt.ToString("yyyy-MM-dd HH:mm"))
            .Where(g => g.Count() > 5)
            .Any();

        if (concurrentFailures)
        {
            factors.Add("Concurrent test execution may be causing resource contention");
        }

        return factors;
    }

    private List<string> GenerateRecommendations(FlakyTestAnalysis analysis)
    {
        var recommendations = new List<string>();

        if (analysis.FlakyTests.Any(t => t.FlakinessScore > 0.7))
        {
            recommendations.Add("Implement test retry mechanism for highly flaky tests");
        }

        if (analysis.TimeInconsistentTests.Any(t => t.CoefficientOfVariation > 1.0))
        {
            recommendations.Add("Investigate tests with high execution time variance");
        }

        if (analysis.EnvironmentFactors.Any(f => f.Contains("path-related")))
        {
            recommendations.Add("Implement file system abstraction layer (Phase 1-3 of test fix plan)");
        }

        if (analysis.FlakyTests.Count > 10)
        {
            recommendations.Add("Consider running flaky tests in isolation or with reduced parallelism");
        }

        return recommendations;
    }

    private async Task LoadHistoricalDataAsync()
    {
        if (!Directory.Exists(_testResultsPath)) return;

        var trxFiles = Directory.GetFiles(_testResultsPath, "*.trx")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(20)
            .ToList();

        foreach (var file in trxFiles)
        {
            try
            {
                var testRun = await ParseTrxFileAsync(file);
                if (testRun != null)
                {
                    _history.Add(testRun);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse TRX file {file}: {ex.Message}");
            }
        }
    }

    private async Task<TestRunHistory?> ParseTrxFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var doc = XDocument.Parse(content);
        
        var ns = doc.Root?.GetDefaultNamespace();
        if (ns == null) return null;

        var testRun = new TestRunHistory
        {
            RunId = Guid.NewGuid().ToString(),
            ExecutedAt = File.GetLastWriteTime(filePath)
        };

        var unitTestResults = doc.Descendants(ns + "UnitTestResult");
        
        foreach (var result in unitTestResults)
        {
            var testName = result.Attribute("testName")?.Value ?? "Unknown";
            var outcome = result.Attribute("outcome")?.Value ?? "Unknown";
            var duration = result.Attribute("duration")?.Value;
            var errorMessage = result.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message")?.Value;

            testRun.TestResults.Add(new TestExecutionRecord
            {
                TestName = testName,
                Success = outcome == "Passed",
                ExecutionTime = ParseDuration(duration),
                ExecutedAt = testRun.ExecutedAt,
                FailureMessage = errorMessage
            });
        }

        return testRun;
    }

    private TimeSpan ParseDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration)) return TimeSpan.Zero;
        
        if (TimeSpan.TryParse(duration, out var result))
        {
            return result;
        }
        
        return TimeSpan.Zero;
    }
}

public class FlakyTestAnalysis
{
    public DateTime AnalyzedAt { get; set; }
    public int TotalRunsAnalyzed { get; set; }
    public bool InsufficientData { get; set; }
    public List<FlakyTestDetails> FlakyTests { get; set; } = new();
    public List<TimeInconsistencyInfo> TimeInconsistentTests { get; set; } = new();
    public List<string> EnvironmentFactors { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

public class FlakyTestDetails
{
    public string TestName { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public int TotalRuns { get; set; }
    public int FailureCount { get; set; }
    public List<string> FailurePatterns { get; set; } = new();
    public List<string> EnvironmentCorrelations { get; set; } = new();
    public TestExecutionRecord? LastFailure { get; set; }
    public double FlakinessScore { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

public class TimeInconsistencyInfo
{
    public string TestName { get; set; } = string.Empty;
    public TimeSpan MeanExecutionTime { get; set; }
    public TimeSpan StandardDeviation { get; set; }
    public double CoefficientOfVariation { get; set; }
    public TimeSpan MinTime { get; set; }
    public TimeSpan MaxTime { get; set; }
    public List<TimeOutlier> Outliers { get; set; } = new();
}

public class TimeOutlier
{
    public int Index { get; set; }
    public TimeSpan Value { get; set; }
    public double DeviationFromMean { get; set; }
}

public class TestRunHistory
{
    public string RunId { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public List<TestExecutionRecord> TestResults { get; set; } = new();
}

public class TestExecutionRecord
{
    public string TestName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string? FailureMessage { get; set; }
}