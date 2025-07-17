# Scanner111 Copilot Instructions

## Project Architecture

**Scanner111** is a C# port of a Python crash log analyzer for Bethesda games. The solution uses a layered architecture with async-first design patterns.

### Core Components

- **Scanner111.Core**: Business logic with async pipeline pattern using TPL, Channels, and IAsyncEnumerable
- **Scanner111.GUI**: Avalonia MVVM application with ReactiveUI 
- **Scanner111.CLI**: Console app using CommandLineParser and Spectre.Console
- **Scanner111.Tests**: xUnit test project

### Key Design Patterns

**Async Pipeline Architecture**: Replace Python's sync/async dual implementation with unified C# async pipeline:
```csharp
// Use IScanPipeline with IAsyncEnumerable for streaming results
public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
    IEnumerable<string> logPaths,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // Producer/consumer pattern with Channels
    var channel = Channel.CreateUnbounded<ScanResult>();
    await Parallel.ForEachAsync(paths, new ParallelOptions { MaxDegreeOfParallelism = options.MaxConcurrency });
}
```

**Analyzer Factory Pattern**: Dynamic analyzer creation with dependency injection:
```csharp
public interface IAnalyzer
{
    string Name { get; }
    bool CanRunInParallel { get; } // Enables parallel/sequential execution
    int Priority { get; } // Lower numbers run first
    Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default);
}
```

**Message Handler Pattern**: Abstract UI communication across GUI/CLI:
```csharp
MessageHandler.Initialize(new GuiMessageService()); // or CliMessageService
MessageHandler.MsgInfo("Processing...", MessageTarget.GuiOnly);
```

## Critical Implementation Requirements

### Python Reference Code
- **Python code in `Code to Port/` is for reference only** - not used in production
- **Preserve "CLASSIC" in all internal strings** - only change project/namespace names to "Scanner111"
- **Match output format exactly** - reports must be identical to reference Python version
- **Keep ClassicScanLogsInfo class name** - referenced throughout codebase
- **Handle file encoding as UTF-8 with ignore errors** like reference implementation

### Async/Await Best Practices
- Use `ConfigureAwait(false)` in library code
- No blocking operations in async methods
- Support cancellation tokens throughout
- Use `Parallel.ForEachAsync` for batch processing
- Implement proper async disposal with `IAsyncDisposable`

### Project Structure Conventions
```
Scanner111.Core/
├── Models/           # CrashLog, ScanResult, Configuration
├── Analyzers/        # IAnalyzer implementations (FormIdAnalyzer, PluginAnalyzer, etc.)
├── Pipeline/         # IScanPipeline, ScanPipelineBuilder, PerformanceMonitor
├── Infrastructure/   # YamlSettingsCache, MessageHandler, GlobalRegistry
```

## Development Workflows

### Build & Test
```bash
dotnet build                          # Build solution
dotnet test                          # Run tests
dotnet run --project Scanner111.CLI -- scan  # Test CLI
```

### Adding New Analyzers
1. Implement `IAnalyzer` interface in `Scanner111.Core/Analyzers/`
2. Set `CanRunInParallel` and `Priority` properties appropriately
3. Add to `AnalyzerFactory.CreateAnalyzers()` method
4. Create corresponding unit tests

### GUI Development (Avalonia)
- Use ReactiveUI patterns: `this.WhenAnyValue(x => x.Property).Subscribe()`
- Implement `INotifyPropertyChanged` via `ReactiveObject`
- Commands: `ReactiveCommand.CreateFromTask()`
- Dark theme with specific color scheme (#2d2d30 background, #0e639c primary)

### CLI Development (Spectre.Console)
- Use `CommandLineParser` with `[Verb]` attributes
- Implement progress with `AnsiConsole.Progress()`
- Color output: `[green]INFO:[/]`, `[yellow]WARNING:[/]`, `[red]ERROR:[/]`

## Key External Dependencies

- **YamlDotNet**: Configuration loading from `CLASSIC Data/databases/*.yaml`
- **System.Data.SQLite**: FormID database lookups
- **Microsoft.Extensions.Caching.Memory**: Result caching
- **ReactiveUI**: MVVM for GUI
- **Spectre.Console**: Rich CLI output
- **CommandLineParser**: CLI argument parsing

## Testing Patterns

Use real crash logs from `sample_logs/` directory for integration tests:
```csharp
[TestMethod]
public async Task ExtractFormIds_ValidFormIds_ReturnsMatches()
{
    var crashLog = new CrashLog
    {
        CallStack = new List<string> { "  Form ID: 0x0001A332" }
    };
    var result = await _analyzer.AnalyzeAsync(crashLog);
    Assert.IsTrue(result.HasFindings);
}
```

## Performance Considerations

- Use `SemaphoreSlim` to limit concurrent operations
- Implement result caching with `IMemoryCache`
- Monitor performance with `IPerformanceMonitor`
- Use `ConcurrentBag` and `ConcurrentDictionary` for thread-safe collections
- Calculate ETA for long-running operations

## Common Pitfalls to Avoid

1. **Don't change report formatting** - must match reference Python output exactly
2. **Don't skip cancellation tokens** in async methods
3. **Don't use blocking I/O** in async code
4. **Don't forget `ConfigureAwait(false)`** in library code
5. **Always dispose resources** with `using` statements
6. **Preserve reference error messages** - users expect same text format
