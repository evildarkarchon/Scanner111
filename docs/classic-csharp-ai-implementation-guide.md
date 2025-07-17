# Scanner 111 C# Implementation Guide for AI Assistants

## Project Overview
Port CLASSIC Python crash log analyzer to C# using Avalonia MVVM framework. The application analyzes Bethesda game crash logs (Buffout 4/Crash Logger format) with both GUI and CLI interfaces.

## Solution Structure

```
Scanner111/
├── Scanner111.sln
├── Scanner111.Core/                 # .NET 8.0 Class Library
├── Scanner111.GUI/                  # .NET 8.0 Avalonia Application  
├── Scanner111.CLI/                  # .NET 8.0 Console Application
└── Scanner111.Tests/                # .NET 8.0 xUnit Test Project
```

## Phase 1: Core Library Foundation ✅

### Checklist: Project Setup
- [x] Create solution: `dotnet new sln -n Scanner111`
- [x] Create projects:
  ```bash
  dotnet new classlib -n Scanner111.Core -f net8.0
  dotnet new avalonia.mvvm -n Scanner111.GUI -f net8.0
  dotnet new console -n Scanner111.CLI -f net8.0
  dotnet new xunit -n Scanner111.Tests -f net8.0
  ```
- [x] Add project references:
  ```bash
  dotnet add Scanner111.GUI reference Scanner111.Core
  dotnet add Scanner111.CLI reference Scanner111.Core
  dotnet add Scanner111.Tests reference Scanner111.Core
  ```
- [x] Install NuGet packages:
  ```xml
  <!-- Scanner111.Core.csproj -->
  <PackageReference Include="YamlDotNet" Version="13.7.1" />
  <PackageReference Include="System.Data.SQLite" Version="1.0.118" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
  
  <!-- Scanner111.GUI.csproj -->
  <PackageReference Include="Avalonia" Version="11.0.6" />
  <PackageReference Include="Avalonia.Desktop" Version="11.0.6" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.6" />
  <PackageReference Include="Avalonia.ReactiveUI" Version="11.0.6" />
  <PackageReference Include="MessageBox.Avalonia" Version="3.1.5" />
  
  <!-- Scanner111.CLI.csproj -->
  <PackageReference Include="CommandLineParser" Version="2.9.1" />
  <PackageReference Include="Spectre.Console" Version="0.48.0" />
  ```

### Checklist: Core Models Implementation

#### Task: Create CrashLog.cs
**File**: `Scanner111.Core/Models/CrashLog.cs`
```csharp
namespace Scanner111.Core.Models;

public class CrashLog
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public List<string> OriginalLines { get; init; } = new();
    public string Content => string.Join("\n", OriginalLines);
    
    // Parsed sections
    public string MainError { get; set; } = string.Empty;
    public List<string> CallStack { get; set; } = new();
    public Dictionary<string, string> Plugins { get; set; } = new(); // filename -> loadOrder
    public string CrashGenVersion { get; set; } = string.Empty;
    public DateTime? CrashTime { get; set; }
    
    // Validation
    public bool IsComplete => Plugins.Count > 0;
    public bool HasError => !string.IsNullOrEmpty(MainError);
}
```
- [ ] Implement CrashLog class
- [ ] Add XML documentation
- [ ] Create unit tests in `Scanner111.Tests/Models/CrashLogTests.cs`

#### Task: Create ScanResult.cs
**File**: `Scanner111.Core/Models/ScanResult.cs`
```csharp
namespace Scanner111.Core.Models;

public class ScanResult
{
    public required string LogPath { get; init; }
    public List<string> Report { get; init; } = new();
    public bool Failed { get; init; }
    public ScanStatistics Statistics { get; init; } = new();
    
    public string ReportText => string.Join("", Report);
    public string OutputPath => Path.ChangeExtension(LogPath, null) + "-AUTOSCAN.md";
}

public class ScanStatistics : Dictionary<string, int>
{
    public ScanStatistics()
    {
        this["scanned"] = 0;
        this["incomplete"] = 0;
        this["failed"] = 0;
    }
}
```
- [ ] Implement ScanResult class
- [ ] Implement ScanStatistics with proper initialization
- [ ] Add convenience methods for statistics

#### Task: Create Configuration Models
**File**: `Scanner111.Core/Models/Configuration.cs`
```csharp
namespace Scanner111.Core.Models;

public class ClassicScanLogsInfo
{
    public string ClassicVersion { get; set; } = "7.35.0";
    public string CrashgenName { get; set; } = "Buffout 4";
    public List<string> ClassicGameHints { get; set; } = new();
    public string AutoscanText { get; set; } = string.Empty;
    
    // Suspect patterns from YAML
    public Dictionary<string, string> SuspectsErrorList { get; set; } = new();
    public Dictionary<string, string> SuspectsStackList { get; set; } = new();
    public List<string> IgnorePluginsList { get; set; } = new();
    
    // Named records patterns
    public Dictionary<string, List<string>> NamedRecordsType { get; set; } = new();
}
```
- [ ] Create all configuration model classes
- [ ] Match Python dataclass structure exactly

### Checklist: Infrastructure Implementation

#### Task: Implement YamlSettingsCache
**File**: `Scanner111.Core/Infrastructure/YamlSettingsCache.cs`
```csharp
namespace Scanner111.Core.Infrastructure;

public static class YamlSettingsCache
{
    private static readonly Dictionary<string, object?> _cache = new();
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();
    
    public static T? YamlSettings<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        var cacheKey = $"{yamlFile}:{keyPath}";
        
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (T?)cached;
        
        try
        {
            var yamlPath = Path.Combine("CLASSIC Data", "databases", $"{yamlFile}.yaml");
            if (!File.Exists(yamlPath))
                return defaultValue;
            
            var yaml = File.ReadAllText(yamlPath);
            var data = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
            
            // Navigate key path (e.g., "CLASSIC_Settings.Show FormID Values")
            var value = NavigateKeyPath(data, keyPath);
            
            _cache[cacheKey] = value;
            return value != null ? (T)value : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public static void SetYamlSetting<T>(string yamlFile, string keyPath, T value)
    {
        // Implementation for saving settings
    }
}
```
- [ ] Implement YAML loading with YamlDotNet
- [ ] Implement key path navigation (dot notation)
- [ ] Add caching mechanism
- [ ] Create unit tests for various data types
- [ ] Handle missing files gracefully

#### Task: Implement MessageHandler
**File**: `Scanner111.Core/Infrastructure/MessageHandler.cs`
```csharp
namespace Scanner111.Core.Infrastructure;

public interface IMessageHandler
{
    void ShowInfo(string message, MessageTarget target = MessageTarget.All);
    void ShowWarning(string message);
    void ShowError(string message);
    IProgress<ProgressInfo> ShowProgress(string title, int totalItems);
}

public enum MessageTarget
{
    All,
    GuiOnly,
    CliOnly
}

public class ProgressInfo
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = string.Empty;
}

public static class MessageHandler
{
    private static IMessageHandler? _handler;
    
    public static void Initialize(IMessageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
    
    public static void MsgInfo(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowInfo(message, target);
    }
    
    public static void MsgWarning(string message)
    {
        _handler?.ShowWarning(message);
    }
    
    public static void MsgError(string message)
    {
        _handler?.ShowError(message);
    }
    
    public static IProgress<ProgressInfo> MsgProgress(string title, int totalItems)
    {
        return _handler?.ShowProgress(title, totalItems) ?? new NullProgress();
    }
}
```
- [ ] Create IMessageHandler interface
- [ ] Implement static MessageHandler class
- [ ] Add all message types (Info, Warning, Error, Progress)
- [ ] Include MessageTarget enum
- [ ] Create NullProgress implementation for safety

#### Task: Implement GlobalRegistry
**File**: `Scanner111.Core/Infrastructure/GlobalRegistry.cs`
```csharp
namespace Scanner111.Core.Infrastructure;

public static class GlobalRegistry
{
    private static readonly Dictionary<string, object> _registry = new();
    
    public static void Set<T>(string key, T value) where T : notnull
    {
        _registry[key] = value;
    }
    
    public static T? Get<T>(string key) where T : class
    {
        return _registry.TryGetValue(key, out var value) ? value as T : null;
    }
    
    // Convenience properties
    public static string Game => Get<string>("Game") ?? "Fallout4";
    public static string GameVR => Get<string>("GameVR") ?? "";
    public static string LocalDir => Get<string>("LocalDir") ?? AppDomain.CurrentDomain.BaseDirectory;
}
```
- [ ] Implement generic registry pattern
- [ ] Add convenience properties for common values
- [ ] Thread-safe implementation if needed

## Phase 2: Analyzer Implementation ✅

### Checklist: FormID Analyzer

#### Task: Create IAnalyzer Interface
**File**: `Scanner111.Core/Analyzers/IAnalyzer.cs`
```csharp
namespace Scanner111.Core.Analyzers;

public interface IAnalyzer
{
    string Name { get; }
    Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default);
}

public abstract class AnalysisResult
{
    public required string AnalyzerName { get; init; }
    public bool HasFindings { get; init; }
    public List<string> ReportLines { get; init; } = new();
}
```
- [ ] Create base analyzer interface
- [ ] Create AnalysisResult base class
- [ ] Add cancellation token support

#### Task: Implement FormIdAnalyzer
**File**: `Scanner111.Core/Analyzers/FormIdAnalyzer.cs`
```csharp
namespace Scanner111.Core.Analyzers;

public class FormIdAnalyzer : IAnalyzer
{
    private static readonly Regex FormIdPattern = new(
        @"^\s*Form ID:\s*0x([0-9A-F]{8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private readonly bool _showFormIdValues;
    private readonly bool _formIdDbExists;
    private readonly FormIdDatabase? _database;
    
    public string Name => "FormID Analyzer";
    
    public FormIdAnalyzer(ClassicScanLogsInfo config, bool showFormIdValues, bool formIdDbExists)
    {
        _showFormIdValues = showFormIdValues;
        _formIdDbExists = formIdDbExists;
        
        if (_formIdDbExists)
        {
            _database = new FormIdDatabase();
        }
    }
    
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        var formIds = ExtractFormIds(crashLog.CallStack);
        var report = new List<string>();
        
        GenerateFormIdReport(formIds, crashLog.Plugins, report);
        
        return new FormIdAnalysisResult
        {
            AnalyzerName = Name,
            FormIds = formIds,
            ReportLines = report,
            HasFindings = formIds.Count > 0
        };
    }
    
    private List<string> ExtractFormIds(List<string> callStack)
    {
        // Direct port of Python logic
        var formIds = new List<string>();
        
        foreach (var line in callStack)
        {
            var match = FormIdPattern.Match(line);
            if (match.Success)
            {
                var formId = match.Groups[1].Value.ToUpper();
                if (!formId.StartsWith("FF"))
                {
                    formIds.Add($"Form ID: {formId}");
                }
            }
        }
        
        return formIds;
    }
}
```
- [ ] Implement FormIdAnalyzer class
- [ ] Port ExtractFormIds method exactly
- [ ] Port FormIdMatch method exactly
- [ ] Add database lookup support
- [ ] Create unit tests with sample data

### Checklist: Other Analyzers

#### Task: Implement PluginAnalyzer
**File**: `Scanner111.Core/Analyzers/PluginAnalyzer.cs`
- [ ] Create PluginAnalyzer class
- [ ] Port plugin_match method
- [ ] Handle ignore list
- [ ] Format output exactly like Python

#### Task: Implement SuspectScanner
**File**: `Scanner111.Core/Analyzers/SuspectScanner.cs`
- [ ] Create SuspectScanner class
- [ ] Port suspect_scan_mainerror method
- [ ] Port suspect_scan_stack method
- [ ] Load patterns from YAML

#### Task: Implement SettingsScanner
**File**: `Scanner111.Core/Analyzers/SettingsScanner.cs`
- [ ] Create SettingsScanner class
- [ ] Port all validation methods
- [ ] Handle FCX mode checks

#### Task: Implement RecordScanner
**File**: `Scanner111.Core/Analyzers/RecordScanner.cs`
- [ ] Create RecordScanner class
- [ ] Port scan_named_records method
- [ ] Handle type-specific patterns

# Phase 3: C# Native Orchestrator Pattern ✅

## Overview
Replace Python's sync/async dual implementation with a unified C# async pipeline that leverages:
- Native async/await without GIL limitations
- Task Parallel Library (TPL) for efficient parallelism
- Channels for producer/consumer patterns
- IAsyncEnumerable for streaming results
- Built-in cancellation and progress reporting

## Checklist: Core Pipeline Components

### Task: Create IScanPipeline Interface
**File**: `Scanner111.Core/Pipeline/IScanPipeline.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public interface IScanPipeline : IAsyncDisposable
{
    /// <summary>
    /// Process a single crash log file
    /// </summary>
    Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process multiple crash logs with parallelism
    /// </summary>
    IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public class ScanOptions
{
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
    public bool PreserveOrder { get; init; } = false;
    public int? MaxDegreeOfParallelism { get; init; }
    public bool EnableCaching { get; init; } = true;
    public TimeSpan? Timeout { get; init; }
}

public class BatchProgress
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SuccessfulScans { get; init; }
    public int FailedScans { get; init; }
    public int IncompleteScans { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public double ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100.0) / TotalFiles : 0;
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}
```
- [ ] Define async-first interface
- [ ] Add streaming support with IAsyncEnumerable
- [ ] Include comprehensive options
- [ ] Design rich progress reporting

### Task: Implement ScanPipeline
**File**: `Scanner111.Core/Pipeline/ScanPipeline.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public class ScanPipeline : IScanPipeline
{
    private readonly ClassicScanLogsInfo _config;
    private readonly IAnalyzerFactory _analyzerFactory;
    private readonly IReportGenerator _reportGenerator;
    private readonly ICrashLogParser _parser;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ScanPipeline> _logger;
    private readonly SemaphoreSlim _semaphore;
    
    public ScanPipeline(
        ClassicScanLogsInfo config,
        IAnalyzerFactory analyzerFactory,
        IReportGenerator reportGenerator,
        ICrashLogParser parser,
        IMemoryCache cache,
        ILogger<ScanPipeline> logger)
    {
        _config = config;
        _analyzerFactory = analyzerFactory;
        _reportGenerator = reportGenerator;
        _parser = parser;
        _cache = cache;
        _logger = logger;
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }
    
    public async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue<ScanResult>(logPath, out var cached))
        {
            _logger.LogDebug("Returning cached result for {LogPath}", logPath);
            return cached;
        }
        
        try
        {
            // Parse crash log
            var crashLog = await _parser.ParseAsync(logPath, cancellationToken);
            
            // Run analyzers in parallel where possible
            var analyzers = _analyzerFactory.CreateAnalyzers(_config);
            var analysisResults = await RunAnalyzersAsync(crashLog, analyzers, cancellationToken);
            
            // Generate report
            var report = await _reportGenerator.GenerateReportAsync(crashLog, analysisResults, cancellationToken);
            
            // Create result
            var result = new ScanResult
            {
                LogPath = logPath,
                Report = report.Lines,
                Failed = report.HasFailures,
                Statistics = CalculateStatistics(crashLog, analysisResults)
            };
            
            // Cache result
            _cache.Set(logPath, result, TimeSpan.FromMinutes(10));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {LogPath}", logPath);
            return new ScanResult
            {
                LogPath = logPath,
                Failed = true,
                Report = new List<string> { $"Error processing log: {ex.Message}" }
            };
        }
    }
    
    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var paths = logPaths.ToList();
        var stats = new ConcurrentDictionary<string, int>();
        stats["processed"] = 0;
        stats["successful"] = 0;
        stats["failed"] = 0;
        stats["incomplete"] = 0;
        
        var startTime = DateTime.UtcNow;
        
        // Create processing channel
        var channel = Channel.CreateUnbounded<ScanResult>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        // Producer task
        var producer = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(
                    paths,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = options.MaxConcurrency,
                        CancellationToken = cancellationToken
                    },
                    async (logPath, ct) =>
                    {
                        await _semaphore.WaitAsync(ct);
                        try
                        {
                            var result = await ProcessSingleAsync(logPath, ct);
                            
                            // Update statistics
                            Interlocked.Increment(ref stats["processed"]);
                            if (result.Failed)
                                Interlocked.Increment(ref stats["failed"]);
                            else if (result.Statistics["incomplete"] > 0)
                                Interlocked.Increment(ref stats["incomplete"]);
                            else
                                Interlocked.Increment(ref stats["successful"]);
                            
                            // Report progress
                            progress?.Report(new BatchProgress
                            {
                                TotalFiles = paths.Count,
                                ProcessedFiles = stats["processed"],
                                SuccessfulScans = stats["successful"],
                                FailedScans = stats["failed"],
                                IncompleteScans = stats["incomplete"],
                                CurrentFile = Path.GetFileName(logPath),
                                ElapsedTime = DateTime.UtcNow - startTime,
                                EstimatedTimeRemaining = CalculateETA(stats["processed"], paths.Count, startTime)
                            });
                            
                            await channel.Writer.WriteAsync(result, ct);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    });
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);
        
        // Consumer - yield results as they become available
        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
        
        await producer;
    }
    
    private async Task<List<AnalysisResult>> RunAnalyzersAsync(
        CrashLog crashLog, 
        IReadOnlyList<IAnalyzer> analyzers,
        CancellationToken cancellationToken)
    {
        // Some analyzers can run in parallel, others must run sequentially
        var parallelAnalyzers = analyzers.Where(a => a.CanRunInParallel).ToList();
        var sequentialAnalyzers = analyzers.Where(a => !a.CanRunInParallel).ToList();
        
        var results = new List<AnalysisResult>();
        
        // Run parallel analyzers
        if (parallelAnalyzers.Any())
        {
            var parallelTasks = parallelAnalyzers
                .Select(analyzer => analyzer.AnalyzeAsync(crashLog, cancellationToken))
                .ToList();
            
            var parallelResults = await Task.WhenAll(parallelTasks);
            results.AddRange(parallelResults);
        }
        
        // Run sequential analyzers
        foreach (var analyzer in sequentialAnalyzers)
        {
            var result = await analyzer.AnalyzeAsync(crashLog, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }
    
    private static TimeSpan? CalculateETA(int processed, int total, DateTime startTime)
    {
        if (processed == 0) return null;
        
        var elapsed = DateTime.UtcNow - startTime;
        var averageTime = elapsed.TotalSeconds / processed;
        var remaining = total - processed;
        
        return TimeSpan.FromSeconds(averageTime * remaining);
    }
    
    public async ValueTask DisposeAsync()
    {
        _semaphore?.Dispose();
        await Task.CompletedTask;
    }
}
```
- [ ] Implement async-first pipeline
- [ ] Use Parallel.ForEachAsync for batch processing
- [ ] Implement producer/consumer with Channels
- [ ] Add memory caching for performance
- [ ] Include proper error handling and logging
- [ ] Support cancellation throughout
- [ ] Calculate and report ETA

### Task: Create Analyzer Factory
**File**: `Scanner111.Core/Pipeline/IAnalyzerFactory.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public interface IAnalyzerFactory
{
    IReadOnlyList<IAnalyzer> CreateAnalyzers(ClassicScanLogsInfo config);
}

public class AnalyzerFactory : IAnalyzerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<ScanSettings> _settings;
    
    public AnalyzerFactory(IServiceProvider serviceProvider, IOptions<ScanSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
    }
    
    public IReadOnlyList<IAnalyzer> CreateAnalyzers(ClassicScanLogsInfo config)
    {
        var analyzers = new List<IAnalyzer>();
        
        // Create analyzers based on settings
        if (_settings.Value.EnablePluginAnalysis)
        {
            analyzers.Add(new PluginAnalyzer(config));
        }
        
        if (_settings.Value.EnableFormIdAnalysis)
        {
            var formIdDb = _serviceProvider.GetService<IFormIdDatabase>();
            analyzers.Add(new FormIdAnalyzer(config, _settings.Value.ShowFormIdValues, formIdDb != null));
        }
        
        if (_settings.Value.EnableSuspectScanning)
        {
            analyzers.Add(new SuspectScanner(config));
        }
        
        if (_settings.Value.EnableRecordScanning)
        {
            analyzers.Add(new RecordScanner(config));
        }
        
        if (_settings.Value.EnableSettingsValidation)
        {
            analyzers.Add(new SettingsScanner(config));
        }
        
        return analyzers;
    }
}
```
- [ ] Create factory pattern for analyzers
- [ ] Support dependency injection
- [ ] Allow dynamic analyzer configuration
- [ ] Support analyzer ordering

### Task: Update IAnalyzer Interface
**File**: `Scanner111.Core/Analyzers/IAnalyzer.cs`
```csharp
namespace Scanner111.Core.Analyzers;

public interface IAnalyzer
{
    string Name { get; }
    bool CanRunInParallel { get; }
    int Priority { get; } // Lower numbers run first
    
    Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default);
}

public abstract class BaseAnalyzer : IAnalyzer
{
    public abstract string Name { get; }
    public virtual bool CanRunInParallel => true;
    public virtual int Priority => 100;
    
    public abstract Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default);
    
    protected static async Task<T> RunWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        
        try
        {
            return await operation(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }
}
```
- [ ] Add parallelization hints
- [ ] Add priority for execution order
- [ ] Create base class with common functionality
- [ ] Add timeout support

### Task: Implement Pipeline Builder
**File**: `Scanner111.Core/Pipeline/ScanPipelineBuilder.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public interface IScanPipelineBuilder
{
    IScanPipelineBuilder WithAnalyzer<TAnalyzer>() where TAnalyzer : IAnalyzer;
    IScanPipelineBuilder WithAnalyzer(IAnalyzer analyzer);
    IScanPipelineBuilder WithAnalyzer(Func<IServiceProvider, IAnalyzer> factory);
    IScanPipelineBuilder WithCache(IMemoryCache cache);
    IScanPipelineBuilder WithOptions(Action<ScanOptions> configure);
    IScanPipelineBuilder WithReportGenerator(IReportGenerator generator);
    IScanPipelineBuilder WithParser(ICrashLogParser parser);
    IScanPipeline Build();
}

public class ScanPipelineBuilder : IScanPipelineBuilder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Func<IServiceProvider, IAnalyzer>> _analyzerFactories = new();
    private IMemoryCache? _cache;
    private IReportGenerator? _reportGenerator;
    private ICrashLogParser? _parser;
    private readonly ScanOptions _options = new();
    
    public ScanPipelineBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IScanPipelineBuilder WithAnalyzer<TAnalyzer>() where TAnalyzer : IAnalyzer
    {
        _analyzerFactories.Add(sp => ActivatorUtilities.CreateInstance<TAnalyzer>(sp));
        return this;
    }
    
    public IScanPipelineBuilder WithAnalyzer(IAnalyzer analyzer)
    {
        _analyzerFactories.Add(_ => analyzer);
        return this;
    }
    
    public IScanPipelineBuilder WithAnalyzer(Func<IServiceProvider, IAnalyzer> factory)
    {
        _analyzerFactories.Add(factory);
        return this;
    }
    
    public IScanPipelineBuilder WithCache(IMemoryCache cache)
    {
        _cache = cache;
        return this;
    }
    
    public IScanPipelineBuilder WithOptions(Action<ScanOptions> configure)
    {
        configure(_options);
        return this;
    }
    
    public IScanPipelineBuilder WithReportGenerator(IReportGenerator generator)
    {
        _reportGenerator = generator;
        return this;
    }
    
    public IScanPipelineBuilder WithParser(ICrashLogParser parser)
    {
        _parser = parser;
        return this;
    }
    
    public IScanPipeline Build()
    {
        var cache = _cache ?? _serviceProvider.GetRequiredService<IMemoryCache>();
        var reportGenerator = _reportGenerator ?? _serviceProvider.GetRequiredService<IReportGenerator>();
        var parser = _parser ?? _serviceProvider.GetRequiredService<ICrashLogParser>();
        var logger = _serviceProvider.GetRequiredService<ILogger<ScanPipeline>>();
        var config = _serviceProvider.GetRequiredService<ClassicScanLogsInfo>();
        
        var analyzerFactory = new CustomAnalyzerFactory(_analyzerFactories, _serviceProvider);
        
        return new ScanPipeline(config, analyzerFactory, reportGenerator, parser, cache, logger);
    }
    
    private class CustomAnalyzerFactory : IAnalyzerFactory
    {
        private readonly List<Func<IServiceProvider, IAnalyzer>> _factories;
        private readonly IServiceProvider _serviceProvider;
        
        public CustomAnalyzerFactory(List<Func<IServiceProvider, IAnalyzer>> factories, IServiceProvider serviceProvider)
        {
            _factories = factories;
            _serviceProvider = serviceProvider;
        }
        
        public IReadOnlyList<IAnalyzer> CreateAnalyzers(ClassicScanLogsInfo config)
        {
            return _factories
                .Select(f => f(_serviceProvider))
                .OrderBy(a => a.Priority)
                .ToList();
        }
    }
}
```
- [ ] Implement fluent builder pattern
- [ ] Support dependency injection
- [ ] Allow custom analyzer registration
- [ ] Configure pipeline options

### Task: Create Concurrent Collections Helper
**File**: `Scanner111.Core/Pipeline/ConcurrentResultsCollector.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public class ConcurrentResultsCollector
{
    private readonly ConcurrentBag<ScanResult> _results = new();
    private readonly ConcurrentDictionary<string, int> _statistics = new();
    private long _totalProcessed;
    private long _totalBytes;
    
    public void AddResult(ScanResult result)
    {
        _results.Add(result);
        Interlocked.Increment(ref _totalProcessed);
        
        foreach (var (key, value) in result.Statistics)
        {
            _statistics.AddOrUpdate(key, value, (_, existing) => existing + value);
        }
    }
    
    public void AddBytes(long bytes)
    {
        Interlocked.Add(ref _totalBytes, bytes);
    }
    
    public ScanSummary GetSummary()
    {
        return new ScanSummary
        {
            Results = _results.ToList(),
            TotalProcessed = _totalProcessed,
            TotalBytes = _totalBytes,
            Statistics = new Dictionary<string, int>(_statistics),
            SuccessRate = CalculateSuccessRate()
        };
    }
    
    private double CalculateSuccessRate()
    {
        if (_totalProcessed == 0) return 0;
        
        var successful = _statistics.GetValueOrDefault("scanned", 0);
        return (successful * 100.0) / _totalProcessed;
    }
}

public class ScanSummary
{
    public List<ScanResult> Results { get; init; } = new();
    public long TotalProcessed { get; init; }
    public long TotalBytes { get; init; }
    public Dictionary<string, int> Statistics { get; init; } = new();
    public double SuccessRate { get; init; }
}
```
- [ ] Create thread-safe results collector
- [ ] Calculate aggregate statistics
- [ ] Support concurrent updates
- [ ] Generate summary reports

### Task: Add Performance Monitoring
**File**: `Scanner111.Core/Pipeline/PerformanceMonitor.cs`
```csharp
namespace Scanner111.Core.Pipeline;

public interface IPerformanceMonitor
{
    void RecordFileProcessed(string fileName, TimeSpan duration, long fileSize);
    void RecordAnalyzerExecution(string analyzerName, TimeSpan duration);
    PerformanceReport GenerateReport();
}

public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ConcurrentBag<FileMetrics> _fileMetrics = new();
    private readonly ConcurrentBag<AnalyzerMetrics> _analyzerMetrics = new();
    private readonly Stopwatch _totalTime = Stopwatch.StartNew();
    
    public void RecordFileProcessed(string fileName, TimeSpan duration, long fileSize)
    {
        _fileMetrics.Add(new FileMetrics
        {
            FileName = fileName,
            Duration = duration,
            FileSize = fileSize,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public void RecordAnalyzerExecution(string analyzerName, TimeSpan duration)
    {
        _analyzerMetrics.Add(new AnalyzerMetrics
        {
            AnalyzerName = analyzerName,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public PerformanceReport GenerateReport()
    {
        var fileMetricsList = _fileMetrics.ToList();
        var analyzerMetricsList = _analyzerMetrics.ToList();
        
        return new PerformanceReport
        {
            TotalDuration = _totalTime.Elapsed,
            FilesProcessed = fileMetricsList.Count,
            TotalBytesProcessed = fileMetricsList.Sum(f => f.FileSize),
            AverageFileProcessingTime = fileMetricsList.Any() 
                ? TimeSpan.FromMilliseconds(fileMetricsList.Average(f => f.Duration.TotalMilliseconds))
                : TimeSpan.Zero,
            FilesPerSecond = fileMetricsList.Count / _totalTime.Elapsed.TotalSeconds,
            AnalyzerPerformance = analyzerMetricsList
                .GroupBy(a => a.AnalyzerName)
                .Select(g => new AnalyzerPerformance
                {
                    AnalyzerName = g.Key,
                    TotalExecutions = g.Count(),
                    AverageExecutionTime = TimeSpan.FromMilliseconds(g.Average(a => a.Duration.TotalMilliseconds)),
                    TotalTime = TimeSpan.FromMilliseconds(g.Sum(a => a.Duration.TotalMilliseconds))
                })
                .OrderByDescending(a => a.TotalTime)
                .ToList()
        };
    }
}
```
- [ ] Track file processing metrics
- [ ] Monitor analyzer performance
- [ ] Calculate throughput statistics
- [ ] Generate performance reports

## Phase 4: GUI Implementation ✅

### Checklist: ViewModels

#### Task: Create ViewModelBase
**File**: `Scanner111.GUI/ViewModels/ViewModelBase.cs`
```csharp
using ReactiveUI;

namespace Scanner111.GUI.ViewModels;

public abstract class ViewModelBase : ReactiveObject
{
}
```

#### Task: Create MainWindowViewModel
**File**: `Scanner111.GUI/ViewModels/MainWindowViewModel.cs`
```csharp
namespace Scanner111.GUI.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly IScanPipeline _pipeline;
    private bool _isScanning;
    private string _statusMessage = "Ready";
    
    // Settings properties
    private bool _showFormIdValues;
    private bool _moveUnsolvedLogs;
    private bool? _fcxMode;
    private int _maxConcurrency = Environment.ProcessorCount;
    
    public MainWindowViewModel()
    {
        // Load settings
        LoadSettings();
        
        // Create commands
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(
            ScanCrashLogsAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));
        
        ScanGameFilesCommand = ReactiveCommand.CreateFromTask(
            ScanGameFilesAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));
        
        // Auto-save settings on change
        this.WhenAnyValue(x => x.ShowFormIdValues)
            .Skip(1) // Skip initial value
            .Subscribe(value => SaveSetting("Show FormID Values", value));
        
        this.WhenAnyValue(x => x.MaxConcurrency)
            .Skip(1)
            .Subscribe(value => SaveSetting("Max Concurrency", value));
    }
    
    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        set => this.RaiseAndSetIfChanged(ref _maxConcurrency, value);
    }
    
    // Commands
    public ReactiveCommand<Unit, Unit> ScanCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanGameFilesCommand { get; }
    
    private async Task ScanCrashLogsAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning crash logs...";
        
        try
        {
            var scanner = new ClassicScanLogs
            {
                FcxMode = _fcxMode,
                MaxConcurrency = _maxConcurrency
            };
            await scanner.CrashLogsScanAsync();
            StatusMessage = "Scan complete!";
        }
        catch (Exception ex)
        {
            MessageHandler.MsgError($"Scan failed: {ex.Message}");
            StatusMessage = "Scan failed";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
```
- [ ] Implement all bindable properties
- [ ] Create all commands
- [ ] Implement settings loading/saving
- [ ] Add progress reporting
- [ ] Handle errors gracefully

### Checklist: Views

#### Task: Create MainWindow.axaml
**File**: `Scanner111.GUI/Views/MainWindow.axaml`
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Scanner111.GUI.ViewModels"
        x:Class="Scanner111.GUI.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Scanner 111 - Crash Log Auto Scanner"
        Width="1200" Height="800"
        MinWidth="800" MinHeight="600">
    
    <Design.DataContext>
        <vm:MainWindowViewModel/>
    </Design.DataContext>
    
    <Grid RowDefinitions="Auto,*,Auto">
        <!-- Header -->
        <Border Grid.Row="0" Background="#2b2b2b" Height="80">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <Image Grid.Column="0" Source="/Assets/logo.png" Height="60" Margin="20,0"/>
                <TextBlock Grid.Column="1" Text="Scanner 111" FontSize="32" 
                           VerticalAlignment="Center" HorizontalAlignment="Center"
                           Foreground="White" FontWeight="Bold"/>
                <TextBlock Grid.Column="2" Text="{Binding Version}" 
                           VerticalAlignment="Center" Margin="20"
                           Foreground="Gray"/>
            </Grid>
        </Border>
        
        <!-- Main Content -->
        <TabControl Grid.Row="1" Margin="10">
            <TabItem Header="Main">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel Spacing="20" Margin="20">
                        <!-- Action Buttons -->
                        <Border Background="#1e1e1e" CornerRadius="5" Padding="20">
                            <StackPanel Spacing="10">
                                <TextBlock Text="Actions" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>
                                
                                <Button Command="{Binding ScanCrashLogsCommand}"
                                        IsEnabled="{Binding !IsScanning}"
                                        HorizontalAlignment="Stretch"
                                        Height="50"
                                        Classes="primary">
                                    <StackPanel Orientation="Horizontal" Spacing="10">
                                        <PathIcon Data="{StaticResource document_search_regular}"/>
                                        <TextBlock Text="SCAN CRASH LOGS" VerticalAlignment="Center"/>
                                    </StackPanel>
                                </Button>
                                
                                <Button Command="{Binding ScanGameFilesCommand}"
                                        IsEnabled="{Binding !IsScanning}"
                                        HorizontalAlignment="Stretch"
                                        Height="50"
                                        Classes="primary">
                                    <StackPanel Orientation="Horizontal" Spacing="10">
                                        <PathIcon Data="{StaticResource folder_search_regular}"/>
                                        <TextBlock Text="SCAN GAME FILES" VerticalAlignment="Center"/>
                                    </StackPanel>
                                </Button>
                            </StackPanel>
                        </Border>
                        
                        <!-- Settings -->
                        <Border Background="#1e1e1e" CornerRadius="5" Padding="20">
                            <StackPanel Spacing="10">
                                <TextBlock Text="Scan Settings" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>
                                
                                <Grid ColumnDefinitions="*,*,*" RowDefinitions="Auto,Auto,Auto,Auto" 
                                      ColumnSpacing="20" RowSpacing="15">
                                    
                                    <CheckBox Grid.Row="0" Grid.Column="0"
                                              Content="Show FormID Values"
                                              IsChecked="{Binding ShowFormIdValues}"/>
                                    
                                    <CheckBox Grid.Row="0" Grid.Column="1"
                                              Content="Move Unsolved Logs"
                                              IsChecked="{Binding MoveUnsolvedLogs}"/>
                                    
                                    <CheckBox Grid.Row="0" Grid.Column="2"
                                              Content="Simplify Logs"
                                              IsChecked="{Binding SimplifyLogs}"/>
                                    
                                    <!-- Add more checkboxes following the grid pattern -->
                                </Grid>
                                
                                <!-- Performance Settings -->
                                <StackPanel Orientation="Horizontal" Spacing="10" Margin="0,10,0,0">
                                    <TextBlock Text="Max Concurrency:" VerticalAlignment="Center"/>
                                    <NumericUpDown Value="{Binding MaxConcurrency}"
                                                   Minimum="1"
                                                   Maximum="32"
                                                   Width="100"/>
                                    <TextBlock Text="(concurrent operations)" 
                                               Foreground="Gray" 
                                               VerticalAlignment="Center"/>
                                </StackPanel>
                            </StackPanel>
                        </Border>
                        
                        <!-- Info Panel -->
                        <Border Background="#1e1e1e" CornerRadius="5" Padding="20">
                            <TextBlock Text="{Binding InfoMessage}"
                                       TextWrapping="Wrap"
                                       LineHeight="20"/>
                        </Border>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>
            
            <TabItem Header="Settings">
                <!-- Settings content -->
            </TabItem>
            
            <TabItem Header="Backup">
                <!-- Backup content -->
            </TabItem>
            
            <TabItem Header="Help">
                <!-- Help content -->
            </TabItem>
        </TabControl>
        
        <!-- Status Bar -->
        <Border Grid.Row="2" Background="#1a1a1a" Height="30">
            <Grid ColumnDefinitions="*,Auto">
                <TextBlock Grid.Column="0" Text="{Binding StatusMessage}" 
                           VerticalAlignment="Center" Margin="10,0"/>
                <ProgressBar Grid.Column="1" 
                             IsVisible="{Binding IsScanning}"
                             IsIndeterminate="True"
                             Width="100" Height="15" Margin="10,0"/>
            </Grid>
        </Border>
    </Grid>
</Window>
```
- [ ] Create responsive grid layout
- [ ] Add all UI elements from Python version
- [ ] Implement proper data binding
- [ ] Add icons and styling
- [ ] Ensure all widgets remain visible when resizing

#### Task: Create App.axaml Styles
**File**: `Scanner111.GUI/App.axaml`
```xml
<Application.Styles>
    <FluentTheme />
    
    <Style Selector="Window">
        <Setter Property="Background" Value="#2d2d30"/>
        <Setter Property="FontFamily" Value="Segoe UI"/>
    </Style>
    
    <Style Selector="Button.primary">
        <Setter Property="Background" Value="#0e639c"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="CornerRadius" Value="4"/>
    </Style>
    
    <Style Selector="Button.primary:pointerover">
        <Setter Property="Background" Value="#1177bb"/>
    </Style>
    
    <Style Selector="CheckBox">
        <Setter Property="Padding" Value="8,4"/>
        <Setter Property="MinHeight" Value="32"/>
    </Style>
    
    <Style Selector="TabItem">
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="Padding" Value="12,8"/>
    </Style>
</Application.Styles>
```
- [ ] Create dark theme styles
- [ ] Style all control types
- [ ] Add hover effects
- [ ] Ensure readability

### Checklist: Services

#### Task: Implement GuiMessageService
**File**: `Scanner111.GUI/Services/GuiMessageService.cs`
```csharp
namespace Scanner111.GUI.Services;

public class GuiMessageService : IMessageHandler
{
    private readonly Window _mainWindow;
    
    public GuiMessageService(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }
    
    public async void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        
        await MessageBox.Avalonia.MessageBoxManager
            .GetMessageBoxStandardWindow("Information", message)
            .ShowDialog(_mainWindow);
    }
    
    public async void ShowError(string message)
    {
        await MessageBox.Avalonia.MessageBoxManager
            .GetMessageBoxStandardWindow("Error", message, ButtonEnum.Ok, Icon.Error)
            .ShowDialog(_mainWindow);
    }
    
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        var dialog = new ProgressDialog
        {
            Title = title,
            Maximum = totalItems
        };
        
        dialog.ShowDialog(_mainWindow);
        
        return new Progress<ProgressInfo>(info =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                dialog.Value = info.Current;
                dialog.Message = info.Message;
                
                if (info.Current >= info.Total)
                {
                    dialog.Close();
                }
            });
        });
    }
}
```
- [ ] Implement all message types
- [ ] Create progress dialog
- [ ] Handle UI thread marshaling
- [ ] Add dialog styling

## Phase 5: CLI Implementation ✅

### Checklist: CLI Setup

#### Task: Create CLI Program
**File**: `Scanner111.CLI/Program.cs`
```csharp
using CommandLine;
using Spectre.Console;

namespace Scanner111.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Initialize CLI message handler
        MessageHandler.Initialize(new CliMessageService());
        
        return await Parser.Default.ParseArguments<ScanOptions, ConfigOptions>(args)
            .MapResult(
                (ScanOptions opts) => RunScan(opts),
                (ConfigOptions opts) => RunConfig(opts),
                errs => Task.FromResult(1)
            );
    }
    
    static async Task<int> RunScan(ScanOptions options)
    {
        AnsiConsole.Write(new FigletText("Scanner 111").Color(Color.Blue));
        AnsiConsole.WriteLine();
        
        var scanner = new ClassicScanLogs
        {
            FcxMode = options.NoFcxMode ? false : options.FcxMode,
            CustomFolder = options.Folder
        };
        
        await scanner.CrashLogsScanAsync();
        return 0;
    }
    
    static async Task<int> RunConfig(ConfigOptions options)
    {
        // Handle config command
        AnsiConsole.WriteLine("Config command not implemented yet");
        return 0;
    }
}

[Verb("scan", HelpText = "Scan crash logs")]
public class ScanOptions
{
    [Option("fcx-mode", Required = false, HelpText = "Enable FCX mode")]
    public bool? FcxMode { get; set; }
    
    [Option("no-fcx-mode", Required = false, HelpText = "Disable FCX mode")]
    public bool NoFcxMode { get; set; }
    
    [Option("folder", Required = false, HelpText = "Custom crash log folder")]
    public string? Folder { get; set; }
}

[Verb("config", HelpText = "Configure application settings")]
public class ConfigOptions
{
    [Option("set", Required = false, HelpText = "Set a configuration value")]
    public string? SetValue { get; set; }
    
    [Option("get", Required = false, HelpText = "Get a configuration value")]
    public string? GetValue { get; set; }
}
```
- [ ] Set up CommandLineParser
- [ ] Add all command-line options
- [ ] Use Spectre.Console for styling
- [ ] Match Python's CLI interface

#### Task: Implement CliMessageService
**File**: `Scanner111.CLI/Services/CliMessageService.cs`
```csharp
namespace Scanner111.CLI.Services;

public class CliMessageService : IMessageHandler
{
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        AnsiConsole.MarkupLine($"[green]INFO:[/] {Markup.Escape(message)}");
    }
    
    public void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]WARNING:[/] {Markup.Escape(message)}");
    }
    
    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]ERROR:[/] {Markup.Escape(message)}");
    }
    
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        var progress = AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            });
        
        ProgressTask? task = null;
        
        progress.StartAsync(async ctx =>
        {
            task = ctx.AddTask(title, maxValue: totalItems);
        });
        
        return new Progress<ProgressInfo>(info =>
        {
            task?.Increment(info.Current - (int)task.Value);
            if (!string.IsNullOrEmpty(info.Message))
            {
                task?.Description = info.Message;
            }
        });
    }
}
```
- [ ] Implement console output with colors
- [ ] Create progress bar with Spectre.Console
- [ ] Handle MessageTarget filtering
- [ ] Format output nicely

## Phase 6: Integration & Testing ✅

### Checklist: Integration Tasks

#### Task: Create ClassicScanLogs Main Class
**File**: `Scanner111.Core/ClassicScanLogs.cs`
```csharp
namespace Scanner111.Core;

public class ClassicScanLogs
{
    private readonly ClassicScanLogsInfo _yamlData;
    private readonly List<string> _crashLogList;
    private readonly Dictionary<string, int> _crashLogStats;
    
    public bool? FcxMode { get; set; }
    public string? CustomFolder { get; set; }
    
    public ClassicScanLogs()
    {
        // Load configuration
        _yamlData = LoadConfiguration();
        _crashLogStats = new Dictionary<string, int>
        {
            ["scanned"] = 0,
            ["incomplete"] = 0,
            ["failed"] = 0
        };
    }
    
    public async Task CrashLogsScanAsync()
    {
        // Direct port of Python's crashlogs_scan()
        FCXModeHandler.ResetFcxChecks();
        
        // Get crash log files
        _crashLogList = await GetCrashLogFiles();
        
        if (_crashLogList.Count == 0)
        {
            MessageHandler.MsgWarning("No crash logs found to scan.");
            return;
        }
        
        MessageHandler.MsgInfo($"Found {_crashLogList.Count} crash logs to scan.");

        await ScanLogsAsync();
    }
}
```
- [ ] Port main scanning logic
- [ ] Implement file discovery
- [ ] Add async pipeline decision logic
- [ ] Handle statistics tracking

#### Task: Write Unit Tests
**File**: `Scanner111.Tests/Analyzers/FormIdAnalyzerTests.cs`
```csharp
[TestClass]
public class FormIdAnalyzerTests
{
    private FormIdAnalyzer _analyzer;
    private ClassicScanLogsInfo _config;
    
    [TestInitialize]
    public void Setup()
    {
        _config = new ClassicScanLogsInfo();
        _analyzer = new FormIdAnalyzer(_config, false, false);
    }
    
    [TestMethod]
    public async Task ExtractFormIds_ValidFormIds_ReturnsMatches()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            CallStack = new List<string>
            {
                "  Form ID: 0x0001A332",
                "  Form ID: 0xFF000000", // Should be skipped
                "  Form ID: 0x00014E45"
            }
        };
        
        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);
        var formIdResult = result as FormIdAnalysisResult;
        
        // Assert
        Assert.IsNotNull(formIdResult);
        Assert.AreEqual(2, formIdResult.FormIds.Count);
        Assert.IsTrue(formIdResult.FormIds.Contains("Form ID: 0001A332"));
        Assert.IsTrue(formIdResult.FormIds.Contains("Form ID: 00014E45"));
    }
}
```
- [ ] Create test project structure
- [ ] Add sample crash logs to test data
- [ ] Write tests for each analyzer
- [ ] Test orchestrator integration
- [ ] Test file I/O operations

### Checklist: Build & Deployment

#### Task: Configure Build
**File**: `Directory.Build.props`
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```
- [ ] Set up consistent build properties
- [ ] Configure nullable reference types
- [ ] Enable implicit usings
- [ ] Set up CI/CD if needed

#### Task: Create Publishing Profiles
- [ ] Windows x64 self-contained
- [ ] Windows x64 framework-dependent
- [ ] Linux x64 (if needed)
- [ ] macOS (if needed)

## Acceptance Criteria for Each Phase

### Phase 1 Complete When:
- [ ] All models compile without errors
- [ ] YamlSettingsCache can load and parse YAML files
- [ ] MessageHandler routes messages correctly
- [ ] GlobalRegistry stores and retrieves values
- [ ] Unit tests pass for infrastructure

### Phase 2 Complete When:
- [ ] All analyzers implement IAnalyzer interface
- [ ] FormID extraction matches Python output exactly
- [ ] Plugin matching works with ignore list
- [ ] All analyzers have unit tests
- [ ] Report formatting matches Python

### Phase 3 Complete When:
- [ ] Pipeline processes crash logs end-to-end with async/await
- [ ] Reports are generated correctly matching Python format
- [ ] Statistics are calculated properly
- [ ] Fully async pipeline without sync/async split
- [ ] Efficient parallelism using TPL and Channels
- [ ] Streaming results with IAsyncEnumerable
- [ ] Built-in caching and performance monitoring
- [ ] Proper cancellation support throughout
- [ ] Rich progress reporting with ETA
- [ ] Thread-safe concurrent processing
- [ ] Memory-efficient for large batches
- [ ] Configurable concurrency limits
- [ ] Performance metrics collection
- [ ] No blocking operations in async code
- [ ] Proper exception handling and logging

### Phase 4 Complete When:
- [ ] GUI launches and displays correctly
- [ ] All buttons and controls are functional
- [ ] Settings save and load properly
- [ ] Window is fully responsive at all sizes
- [ ] Progress dialogs work during scanning

### Phase 5 Complete When:
- [ ] CLI accepts all command-line arguments
- [ ] Console output is formatted nicely
- [ ] Progress bars display correctly
- [ ] Can run without GUI dependencies
- [ ] Exit codes are appropriate

### Phase 6 Complete When:
- [ ] All tests pass
- [ ] Can scan real crash logs
- [ ] Performance is acceptable
- [ ] No memory leaks
- [ ] Builds can be published

## Notes for AI Implementation

1. **Always check existing Python code** before implementing a feature
2. **Match output format exactly** - reports should be identical to Python version
3. **Use async/await properly** - ConfigureAwait(false) in library code
4. **Handle file encoding** - UTF-8 with ignore errors like Python
5. **Preserve all error messages** - users expect same messages
6. **Keep ClassicScanLogsInfo name** - it's referenced throughout the codebase
7. **Maintain "CLASSIC" in internal strings** - only change project/namespace names

## Common Pitfalls to Avoid

1. Don't change report formatting - it must match Python exactly
2. Don't skip error handling - port Python's try/except blocks
3. Don't forget cancellation tokens in async methods
4. Don't use blocking I/O in async methods
5. Don't forget to dispose resources (use `using` statements)
6. Keep internal references to "CLASSIC" in strings and messages

## Resources

- Python source: `/Code to Port/` directory
- Sample logs: `/sample_logs/` directory  
- YAML files: `/Code to Port/CLASSIC Data/databases/`
- Test data: Use actual crash logs for testing

## Project Name Mapping

- Solution: `Scanner111.sln`
- Core Library: `Scanner111.Core`
- GUI Application: `Scanner111.GUI`  
- CLI Application: `Scanner111.CLI`
- Test Project: `Scanner111.Tests`
- Window Title: "Scanner 111 - Crash Log Auto Scanner"
- CLI Display: "Scanner 111" in FigletText

Keep all other references (class names, YAML keys, report text) as "CLASSIC" to maintain compatibility with configuration files and user expectations.

## Project Name Mapping

- Solution: `Scanner111.sln`
- Core Library: `Scanner111.Core`
- GUI Application: `Scanner111.GUI`  
- CLI Application: `Scanner111.CLI`
- Test Project: `Scanner111.Tests`
- Window Title: "Scanner 111 - Crash Log Auto Scanner"
- CLI Display: "Scanner 111" in FigletText

Keep all other references (class names, YAML keys, report text) as "CLASSIC" to maintain compatibility with configuration files and user expectations.
