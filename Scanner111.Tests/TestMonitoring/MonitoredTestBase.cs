using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Scanner111.Tests.TestMonitoring;

public abstract class MonitoredTestBase : IDisposable
{
    private readonly TestExecutionListener _executionListener;
    private readonly Stopwatch _testStopwatch;
    private readonly string _testClassName;
    private bool _disposed;

    protected ITestOutputHelper Output { get; }

    protected MonitoredTestBase(ITestOutputHelper output)
    {
        Output = output;
        _testClassName = GetType().Name;
        _testStopwatch = Stopwatch.StartNew();
        _executionListener = new TestExecutionListener(output, _testClassName);
        
        Output.WriteLine($"Starting test class: {_testClassName}");
    }

    protected void LogTestStep(string message, [CallerMemberName] string testName = "")
    {
        Output.WriteLine($"[{testName}] {message} (elapsed: {_testStopwatch.Elapsed:mm\\:ss\\.fff})");
    }

    protected void LogPerformanceMetric(string metricName, double value, string unit = "ms", [CallerMemberName] string testName = "")
    {
        Output.WriteLine($"[PERF] {testName}.{metricName}: {value:F2} {unit}");
    }

    protected async Task<T> MeasureAsync<T>(Func<Task<T>> operation, string operationName, [CallerMemberName] string testName = "")
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await operation();
            stopwatch.Stop();
            LogPerformanceMetric(operationName, stopwatch.ElapsedMilliseconds, "ms", testName);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Output.WriteLine($"[ERROR] {operationName} failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            throw;
        }
    }

    protected T Measure<T>(Func<T> operation, string operationName, [CallerMemberName] string testName = "")
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = operation();
            stopwatch.Stop();
            LogPerformanceMetric(operationName, stopwatch.ElapsedMilliseconds, "ms", testName);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Output.WriteLine($"[ERROR] {operationName} failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            throw;
        }
    }

    protected void AssertExecutionTime(TimeSpan actualTime, TimeSpan maxExpectedTime, [CallerMemberName] string testName = "")
    {
        if (actualTime > maxExpectedTime)
        {
            Output.WriteLine($"[WARNING] Test {testName} exceeded expected execution time: {actualTime:g} > {maxExpectedTime:g}");
        }
    }

    protected void RecordTestCompletion(bool success, string? failureReason = null, [CallerMemberName] string testName = "")
    {
        _testStopwatch.Stop();
        var fullTestName = $"{_testClassName}.{testName}";
        _executionListener.Complete(success, failureReason);
        
        Output.WriteLine($"Test {fullTestName} completed in {_testStopwatch.Elapsed:g} - {(success ? "PASSED" : "FAILED")}");
        
        if (!string.IsNullOrEmpty(failureReason))
        {
            Output.WriteLine($"Failure reason: {failureReason}");
        }
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _testStopwatch.Stop();
            Output.WriteLine($"Test class {_testClassName} total execution time: {_testStopwatch.Elapsed:g}");
            _disposed = true;
        }
    }
}

public abstract class MonitoredAsyncTestBase : MonitoredTestBase, IAsyncDisposable
{
    protected MonitoredAsyncTestBase(ITestOutputHelper output) : base(output)
    {
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Dispose();
    }
}

public class TestTimeoutHelper
{
    public static TimeSpan GetTimeout(TestComplexity complexity) => complexity switch
    {
        TestComplexity.Simple => TimeSpan.FromSeconds(5),
        TestComplexity.Medium => TimeSpan.FromSeconds(10),
        TestComplexity.Complex => TimeSpan.FromSeconds(30),
        TestComplexity.LongRunning => TimeSpan.FromMinutes(2),
        _ => TimeSpan.FromSeconds(10)
    };

    public static int GetTimeoutMilliseconds(TestComplexity complexity) => 
        (int)GetTimeout(complexity).TotalMilliseconds;
}

public enum TestComplexity
{
    Simple,
    Medium,
    Complex,
    LongRunning
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComplexityAttribute : Attribute
{
    public TestComplexity Complexity { get; }
    
    public ComplexityAttribute(TestComplexity complexity)
    {
        Complexity = complexity;
    }
}