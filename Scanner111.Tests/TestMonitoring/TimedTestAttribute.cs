using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Scanner111.Tests.TestMonitoring;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TimedTestAttribute : FactAttribute
{
    public int TimeoutMilliseconds { get; set; } = 30000;
    public bool WarnOnSlowExecution { get; set; } = true;
    public int SlowExecutionThresholdMs { get; set; } = 5000;
}

public class TimedTestRunner : XunitTestRunner
{
    private readonly TestPerformanceTracker _performanceTracker;

    public TimedTestRunner(
        ITest test,
        IMessageBus messageBus,
        Type testClass,
        object[] constructorArguments,
        MethodInfo testMethod,
        object[] testMethodArguments,
        string skipReason,
        IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        : base(test, messageBus, testClass, constructorArguments, testMethod, 
               testMethodArguments, skipReason, beforeAfterAttributes, aggregator, 
               cancellationTokenSource)
    {
        _performanceTracker = new TestPerformanceTracker();
    }

    protected override async Task<Tuple<decimal, string>> InvokeTestAsync(ExceptionAggregator aggregator)
    {
        var stopwatch = Stopwatch.StartNew();
        var testName = $"{TestClass.Name}.{TestMethod.Name}";
        string? failureReason = null;
        bool success = true;

        try
        {
            var result = await base.InvokeTestAsync(aggregator);
            stopwatch.Stop();

            if (aggregator.HasExceptions)
            {
                success = false;
                failureReason = aggregator.ToException()?.Message;
            }

            var timedAttribute = TestMethod.GetCustomAttribute<TimedTestAttribute>();
            
            if (timedAttribute != null)
            {
                if (stopwatch.ElapsedMilliseconds > timedAttribute.TimeoutMilliseconds)
                {
                    aggregator.Add(new TimeoutException(
                        $"Test execution exceeded timeout of {timedAttribute.TimeoutMilliseconds}ms " +
                        $"(actual: {stopwatch.ElapsedMilliseconds}ms)"));
                }
                else if (timedAttribute.WarnOnSlowExecution && 
                         stopwatch.ElapsedMilliseconds > timedAttribute.SlowExecutionThresholdMs)
                {
                    MessageBus.QueueMessage(new DiagnosticMessage(
                        $"SLOW TEST WARNING: {testName} took {stopwatch.ElapsedMilliseconds}ms " +
                        $"(threshold: {timedAttribute.SlowExecutionThresholdMs}ms)"));
                }
            }

            _performanceTracker.RecordTestExecution(testName, stopwatch.Elapsed, success, failureReason);
            await _performanceTracker.SaveMetricsAsync();

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _performanceTracker.RecordTestExecution(testName, stopwatch.Elapsed, false, ex.Message);
            await _performanceTracker.SaveMetricsAsync();
            throw;
        }
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class PerformanceCriticalAttribute : Attribute
{
    public int MaxExecutionTimeMs { get; set; } = 1000;
    public string Justification { get; set; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RetryOnFailureAttribute : Attribute
{
    public int MaxRetries { get; set; } = 3;
    public int DelayBetweenRetriesMs { get; set; } = 1000;
    public Type[] RetryOnExceptionTypes { get; set; } = Array.Empty<Type>();
}

public class RetryTestRunner : XunitTestRunner
{
    public RetryTestRunner(
        ITest test,
        IMessageBus messageBus,
        Type testClass,
        object[] constructorArguments,
        MethodInfo testMethod,
        object[] testMethodArguments,
        string skipReason,
        IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        : base(test, messageBus, testClass, constructorArguments, testMethod,
               testMethodArguments, skipReason, beforeAfterAttributes, aggregator,
               cancellationTokenSource)
    {
    }

    protected override async Task<Tuple<decimal, string>> InvokeTestAsync(ExceptionAggregator aggregator)
    {
        var retryAttribute = TestMethod.GetCustomAttribute<RetryOnFailureAttribute>();
        
        if (retryAttribute == null)
        {
            return await base.InvokeTestAsync(aggregator);
        }

        Exception? lastException = null;
        
        for (int attempt = 0; attempt <= retryAttribute.MaxRetries; attempt++)
        {
            var attemptAggregator = new ExceptionAggregator();
            
            try
            {
                var result = await base.InvokeTestAsync(attemptAggregator);
                
                if (!attemptAggregator.HasExceptions)
                {
                    if (attempt > 0)
                    {
                        MessageBus.QueueMessage(new DiagnosticMessage(
                            $"Test succeeded on retry attempt {attempt + 1}"));
                    }
                    return result;
                }

                lastException = attemptAggregator.ToException();
                
                if (retryAttribute.RetryOnExceptionTypes.Length > 0)
                {
                    var shouldRetry = false;
                    foreach (var exType in retryAttribute.RetryOnExceptionTypes)
                    {
                        if (exType.IsInstanceOfType(lastException))
                        {
                            shouldRetry = true;
                            break;
                        }
                    }
                    
                    if (!shouldRetry)
                    {
                        aggregator.Add(lastException);
                        return result;
                    }
                }

                if (attempt < retryAttribute.MaxRetries)
                {
                    MessageBus.QueueMessage(new DiagnosticMessage(
                        $"Test failed on attempt {attempt + 1}, retrying after {retryAttribute.DelayBetweenRetriesMs}ms..."));
                    
                    await Task.Delay(retryAttribute.DelayBetweenRetriesMs);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt == retryAttribute.MaxRetries)
                {
                    aggregator.Add(ex);
                    throw;
                }
                
                await Task.Delay(retryAttribute.DelayBetweenRetriesMs);
            }
        }

        if (lastException != null)
        {
            aggregator.Add(lastException);
        }

        return new Tuple<decimal, string>(0, string.Empty);
    }
}

public class TestExecutionListener : ITestOutputHelper
{
    private readonly ITestOutputHelper _innerOutput;
    private readonly TestPerformanceTracker _tracker;
    private readonly Stopwatch _stopwatch;
    private readonly string _testName;

    public TestExecutionListener(ITestOutputHelper innerOutput, string testName)
    {
        _innerOutput = innerOutput;
        _testName = testName;
        _tracker = new TestPerformanceTracker();
        _stopwatch = Stopwatch.StartNew();
    }

    public void WriteLine(string message)
    {
        _innerOutput.WriteLine($"[{_stopwatch.Elapsed:mm\\:ss\\.fff}] {message}");
    }

    public void WriteLine(string format, params object[] args)
    {
        _innerOutput.WriteLine($"[{_stopwatch.Elapsed:mm\\:ss\\.fff}] {string.Format(format, args)}");
    }

    public void Complete(bool success, string? failureReason = null)
    {
        _stopwatch.Stop();
        _tracker.RecordTestExecution(_testName, _stopwatch.Elapsed, success, failureReason);
        _tracker.SaveMetricsAsync().GetAwaiter().GetResult();
    }
}