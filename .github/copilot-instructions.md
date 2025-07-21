# GitHub Copilot Instructions for Scanner111

## Project Overview
Scanner111 is a C# port of a Python crash log analyzer for Bethesda games (Fallout 4, Skyrim). It analyzes crash dumps to identify problematic game modifications using multiple analyzer engines in a streaming pipeline architecture.

## Core Architecture Patterns

### Async Streaming Pipeline (Critical)
- **All operations use `IAsyncEnumerable<T>`** for streaming results, never collect to lists
- **Producer/consumer with `System.Threading.Channels`** for decoupled async processing
- **`IScanPipeline` interface** at `Scanner111.Core/Pipeline/` - decorator pattern with performance monitoring
- **Example**: `PerformanceMonitoringPipeline` wraps `ScanPipeline` for metrics collection

```csharp
// Correct pattern - streaming with IAsyncEnumerable
await foreach (var result in pipeline.ProcessBatchAsync(logPaths, options, progress, cancellationToken))
{
    yield return result; // Stream immediately, don't accumulate
}
```

### Analyzer Factory Pattern
- **`IAnalyzer` interface** with `Priority`, `CanRunInParallel`, `Name` properties
- **Analyzers**: `FormIdAnalyzer`, `PluginAnalyzer`, `RecordScanner`, `SettingsScanner`, `SuspectScanner`
- **Execution order**: Priority-based with parallel execution support for independent analyzers
- **Location**: `Scanner111.Core/Analyzers/`

### Message Handler Abstraction
- **UI-agnostic communication** via `IMessageHandler`
- **GUI**: `MessageHandler` in `Scanner111.GUI/Services/`
- **CLI**: `CliMessageHandler` in `Scanner111.Core/Infrastructure/`
- **Purpose**: Decouples business logic from UI concerns

## Critical Implementation Requirements

### String Preservation Rules
- **Output format must match Python reference exactly** - see `crash-*-AUTOSCAN.md` files in root

### File I/O Standards
- **Always UTF-8 encoding** with error handling for corrupted game files
- **Use `async` file operations** throughout - no blocking I/O
- **Pattern**: `File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken)`

### Dependency Injection
- **Constructor injection** for all services - no static dependencies
- **`IApplicationSettingsService`** replaces removed `GlobalRegistry`
- **Registration**: Use `.AddTransient<>()`, `.AddSingleton<>()` in startup

## Project Structure

```
Scanner111.Core/          # Business logic library
├── Analyzers/            # IAnalyzer implementations
├── Infrastructure/       # Cross-cutting services
├── Models/              # Domain models (CrashLog, ScanResult)
└── Pipeline/            # IScanPipeline and builders

Scanner111.GUI/          # Avalonia MVVM desktop app
├── ViewModels/          # MVVM view models
├── Views/               # AXAML views
└── Services/            # GUI-specific services

Scanner111.CLI/          # Console app with CommandLineParser
├── Commands/            # ICommand implementations
└── Models/              # CLI-specific options

Scanner111.Tests/        # xUnit test project
```

## Development Workflows

### Testing with Sample Data
```bash
# Use root-level crash logs for testing - each has expected AUTOSCAN.md output
dotnet test --filter "ClassName=FormIdAnalyzerTests"

# Verify output matches expected format in crash-*-AUTOSCAN.md files
dotnet run --project Scanner111.CLI -- scan -l "crash-2023-09-15-01-54-49.log"
```

### Building and Running
```bash
# Build solution
dotnet build

# Run GUI (default)
dotnet run --project Scanner111.GUI

# Run CLI with specific options
dotnet run --project Scanner111.CLI -- scan -l "path/to/crash.log" --verbose
```

## Domain-Specific Patterns

### Crash Log Analysis Flow
1. **Parse crash log** via `CrashLogParser` (UTF-8, error-tolerant)
2. **Run analyzers** in priority order with parallel execution where possible
3. **Generate report** matching Python output format exactly
4. **Stream results** via `IAsyncEnumerable` - never accumulate in memory

### Bethesda Game Integration
- **FormID database lookups** via `FormIdDatabaseService`
- **Game path detection** via `GamePathDetection` service
- **YAML settings cache** via `YamlSettingsCache` for mod compatibility data
- **Data files**: `Data/` directory contains game-specific lookup tables

### Performance Monitoring
- **Decorator pattern**: `PerformanceMonitoringPipeline` wraps base pipeline
- **Metrics collection**: Track per-file and batch processing times
- **Logging**: Use `ILogger` abstraction, structured logging with timing data

## Reference Implementation
- **Python source**: `Code to Port/` directory contains original implementation
- **Expected outputs**: Root-level `crash-*-AUTOSCAN.md` files show exact format requirements
- **Sample logs**: Use for testing - outputs must match exactly

## UI Patterns

### MVVM (GUI)
- **ViewModels**: Inherit from base classes with `INotifyPropertyChanged`
- **Commands**: Use `ReactiveCommand` for async operations
- **Theme**: Dark theme with `#2d2d30` background, `#0e639c` primary color

### CLI Commands
- **CommandLineParser**: Use verbs pattern (`scan`, `demo`, `config`, `about`)
- **Options classes**: In `Scanner111.CLI/Models/` with proper attributes
- **Async execution**: All commands implement `ICommand` with async execution
