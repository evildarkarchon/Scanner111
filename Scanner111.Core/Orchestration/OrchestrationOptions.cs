using System;

namespace Scanner111.Core.Orchestration;

/// <summary>
/// Configuration options for orchestration behavior.
/// </summary>
public sealed class OrchestrationOptions
{
    /// <summary>
    /// Gets or sets the execution strategy to use.
    /// </summary>
    public ExecutionStrategy Strategy { get; set; } = ExecutionStrategy.Parallel;
    
    /// <summary>
    /// Gets or sets the maximum degree of parallelism.
    /// Default is -1 (use system default).
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = -1;
    
    /// <summary>
    /// Gets or sets whether to continue on analyzer failures.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the global timeout for all analyzers.
    /// </summary>
    public TimeSpan? GlobalTimeout { get; set; }
    
    /// <summary>
    /// Gets or sets whether to include timing information in the report.
    /// </summary>
    public bool IncludeTimingInfo { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to generate verbose output.
    /// </summary>
    public bool VerboseOutput { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to enable retry logic for failed analyzers.
    /// </summary>
    public bool EnableRetry { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Gets or sets whether to collect detailed metrics.
    /// </summary>
    public bool CollectMetrics { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to validate analyzer dependencies.
    /// </summary>
    public bool ValidateDependencies { get; set; } = true;
    
    /// <summary>
    /// Creates default options for development/debugging.
    /// </summary>
    public static OrchestrationOptions Development => new()
    {
        Strategy = ExecutionStrategy.Sequential,
        VerboseOutput = true,
        CollectMetrics = true,
        IncludeTimingInfo = true
    };
    
    /// <summary>
    /// Creates default options for production use.
    /// </summary>
    public static OrchestrationOptions Production => new()
    {
        Strategy = ExecutionStrategy.Parallel,
        MaxDegreeOfParallelism = Environment.ProcessorCount,
        EnableRetry = true,
        ContinueOnError = true,
        VerboseOutput = false
    };
}

/// <summary>
/// Defines the execution strategy for running analyzers.
/// </summary>
public enum ExecutionStrategy
{
    /// <summary>
    /// Run all analyzers in parallel.
    /// </summary>
    Parallel,
    
    /// <summary>
    /// Run analyzers sequentially.
    /// </summary>
    Sequential,
    
    /// <summary>
    /// Run analyzers in priority groups.
    /// </summary>
    Prioritized,
    
    /// <summary>
    /// Run analyzers with dependency resolution.
    /// </summary>
    DependencyBased
}