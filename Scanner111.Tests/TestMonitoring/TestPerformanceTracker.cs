using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Scanner111.Tests.TestMonitoring;

public class TestPerformanceTracker
{
    private readonly ConcurrentDictionary<string, TestExecutionMetrics> _metrics = new();
    private readonly string _outputPath;

    public TestPerformanceTracker(string outputPath = "TestResults/performance-metrics.json")
    {
        _outputPath = outputPath;
    }

    public void RecordTestExecution(string testName, TimeSpan duration, bool success, string? failureReason = null)
    {
        _metrics.AddOrUpdate(testName,
            key => new TestExecutionMetrics
            {
                TestName = testName,
                ExecutionTimes = new List<long> { duration.Milliseconds },
                TotalRuns = 1,
                FailureCount = success ? 0 : 1,
                LastRunTime = DateTime.UtcNow,
                LastFailureReason = failureReason
            },
            (key, existing) =>
            {
                existing.ExecutionTimes.Add(duration.Milliseconds);
                existing.TotalRuns++;
                if (!success)
                {
                    existing.FailureCount++;
                    existing.LastFailureReason = failureReason;
                }
                existing.LastRunTime = DateTime.UtcNow;
                return existing;
            });
    }

    public TestHealthReport GenerateHealthReport()
    {
        var allMetrics = _metrics.Values.ToList();
        
        return new TestHealthReport
        {
            GeneratedAt = DateTime.UtcNow,
            TotalTests = allMetrics.Count,
            TotalRuns = allMetrics.Sum(m => m.TotalRuns),
            AverageExecutionTime = TimeSpan.FromMilliseconds(
                allMetrics.SelectMany(m => m.ExecutionTimes).DefaultIfEmpty(0).Average()),
            SlowestTests = allMetrics
                .OrderByDescending(m => m.AverageExecutionTime)
                .Take(10)
                .Select(m => new SlowTestInfo
                {
                    TestName = m.TestName,
                    AverageTime = TimeSpan.FromMilliseconds(m.AverageExecutionTime),
                    MaxTime = TimeSpan.FromMilliseconds(m.MaxExecutionTime)
                })
                .ToList(),
            FlakyTests = DetectFlakyTests(allMetrics),
            FailureRate = allMetrics.Any() 
                ? (double)allMetrics.Sum(m => m.FailureCount) / allMetrics.Sum(m => m.TotalRuns) 
                : 0,
            TestsWithPathIssues = allMetrics
                .Where(m => m.LastFailureReason?.Contains("path", StringComparison.OrdinalIgnoreCase) == true ||
                           m.LastFailureReason?.Contains("directory", StringComparison.OrdinalIgnoreCase) == true ||
                           m.LastFailureReason?.Contains("file", StringComparison.OrdinalIgnoreCase) == true)
                .Select(m => m.TestName)
                .ToList(),
            TestsWithExternalDependencies = allMetrics
                .Where(m => m.LastFailureReason?.Contains("network", StringComparison.OrdinalIgnoreCase) == true ||
                           m.LastFailureReason?.Contains("database", StringComparison.OrdinalIgnoreCase) == true ||
                           m.LastFailureReason?.Contains("service", StringComparison.OrdinalIgnoreCase) == true)
                .Select(m => m.TestName)
                .ToList()
        };
    }

    private List<FlakyTestInfo> DetectFlakyTests(List<TestExecutionMetrics> metrics)
    {
        var flakyTests = new List<FlakyTestInfo>();

        foreach (var metric in metrics.Where(m => m.TotalRuns >= 3))
        {
            var successRate = 1.0 - ((double)metric.FailureCount / metric.TotalRuns);
            
            if (successRate > 0.1 && successRate < 0.9)
            {
                var timeVariance = CalculateVariance(metric.ExecutionTimes);
                var avgTime = metric.ExecutionTimes.Average();
                var coefficientOfVariation = avgTime > 0 ? Math.Sqrt(timeVariance) / avgTime : 0;

                flakyTests.Add(new FlakyTestInfo
                {
                    TestName = metric.TestName,
                    SuccessRate = successRate,
                    TimeVarianceCoefficient = coefficientOfVariation,
                    FlakinessScore = CalculateFlakinessScore(successRate, coefficientOfVariation)
                });
            }
        }

        return flakyTests.OrderByDescending(t => t.FlakinessScore).ToList();
    }

    private double CalculateVariance(List<long> values)
    {
        if (values.Count <= 1) return 0;
        
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return sumOfSquares / (values.Count - 1);
    }

    private double CalculateFlakinessScore(double successRate, double timeVariance)
    {
        var intermittencyScore = 1 - Math.Abs(0.5 - successRate) * 2;
        return (intermittencyScore * 0.7) + (Math.Min(timeVariance, 1.0) * 0.3);
    }

    public async Task SaveMetricsAsync()
    {
        var directory = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_metrics, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_outputPath, json);
    }

    public async Task LoadMetricsAsync()
    {
        if (!File.Exists(_outputPath)) return;

        var json = await File.ReadAllTextAsync(_outputPath);
        var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<string, TestExecutionMetrics>>(json);
        
        if (loaded != null)
        {
            _metrics.Clear();
            foreach (var kvp in loaded)
            {
                _metrics[kvp.Key] = kvp.Value;
            }
        }
    }
}

public class TestExecutionMetrics
{
    public string TestName { get; set; } = string.Empty;
    public List<long> ExecutionTimes { get; set; } = new();
    public int TotalRuns { get; set; }
    public int FailureCount { get; set; }
    public DateTime LastRunTime { get; set; }
    public string? LastFailureReason { get; set; }
    
    public double AverageExecutionTime => ExecutionTimes.Any() ? ExecutionTimes.Average() : 0;
    public long MaxExecutionTime => ExecutionTimes.Any() ? ExecutionTimes.Max() : 0;
    public long MinExecutionTime => ExecutionTimes.Any() ? ExecutionTimes.Min() : 0;
}

public class TestHealthReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalTests { get; set; }
    public int TotalRuns { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public List<SlowTestInfo> SlowestTests { get; set; } = new();
    public List<FlakyTestInfo> FlakyTests { get; set; } = new();
    public double FailureRate { get; set; }
    public List<string> TestsWithPathIssues { get; set; } = new();
    public List<string> TestsWithExternalDependencies { get; set; } = new();
}

public class SlowTestInfo
{
    public string TestName { get; set; } = string.Empty;
    public TimeSpan AverageTime { get; set; }
    public TimeSpan MaxTime { get; set; }
}

public class FlakyTestInfo
{
    public string TestName { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public double TimeVarianceCoefficient { get; set; }
    public double FlakinessScore { get; set; }
}