# Architecture Patterns in Scanner111

## Analyzer Pattern
The core of the system uses an Analyzer Pattern with orchestrated execution:

### Key Components
1. **IAnalyzer Interface**: Base contract for all analyzers
2. **AnalyzerBase**: Abstract base class providing:
   - Validation logic
   - Error handling
   - Report fragment generation
   - Logging infrastructure
   
3. **AnalysisContext**: Shared state container
   - Thread-safe data sharing via SetSharedData/GetSharedData
   - Carries log file path, game type, configuration
   - Accumulates results from multiple analyzers

4. **AnalyzerOrchestrator**: Coordinates analyzer execution
   - Sequential, parallel, or prioritized strategies
   - Error aggregation and reporting
   - Progress tracking

## Execution Strategies
Located in `Orchestration/ExecutionStrategies/`:
- **SequentialStrategy**: Run analyzers one by one
- **ParallelStrategy**: Run compatible analyzers concurrently
- **PrioritizedStrategy**: Run based on priority scores

## Report Fragment Composition
Hierarchical report building system:
- **ReportFragment**: Base unit of report output
- **ReportBuilder**: Assembles fragments into complete reports
- **Nested structure**: Fragments can contain child fragments
- **Multiple output formats**: Text, HTML, JSON

## Caching Patterns
### Static Cross-Instance Caching
```csharp
private static readonly ConcurrentDictionary<string, CachedData> _cache
private static readonly SemaphoreSlim _cacheLock
```
Used in FcxModeHandler for global coordination

### Instance-Level Caching
Individual analyzers maintain their own caches for:
- Parsed configuration data
- File system lookups
- Database query results

## Dependency Injection Architecture
### Service Lifetimes
- **Singleton**: Orchestrator, configuration services
- **Scoped**: Database connections, analysis context
- **Transient**: Individual analyzers, report builders

### Registration Pattern
```csharp
services.AddSingleton<IAnalyzerOrchestrator, AnalyzerOrchestrator>();
services.AddTransient<IAnalyzer, PluginAnalyzer>();
```

## Data Flow
1. **Input**: Log file path, game type
2. **Discovery**: Path detection, game identification
3. **Configuration**: Load YAML settings
4. **Analysis**: Run analyzer pipeline
5. **Aggregation**: Combine results
6. **Reporting**: Generate output

## Async Coordination Patterns
### SemaphoreSlim for Async Locks
```csharp
await _semaphore.WaitAsync(cancellationToken);
try
{
    // Critical section
}
finally
{
    _semaphore.Release();
}
```

### Global Static Coordination
FcxModeHandler uses static SemaphoreSlim for cross-instance caching

## YAML Configuration Architecture
### Store Types (YamlStore enum)
- Main: Primary configuration
- Game: Game-specific settings
- Settings: User preferences
- Database: FormID mappings

### Loading Pattern
```csharp
var settings = await AsyncYamlSettingsCore.LoadYamlAsync<T>(
    gameName, storeType, cancellationToken);
```

## Security Patterns
### Input Validation
- Whitelist validation for SQL table names
- Path sanitization
- Configuration value constraints

### Resource Limits
- Connection pooling with max connections
- Semaphore-based concurrency limits
- Timeout enforcement