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
- **Core Analyzers**: `FormIdAnalyzer`, `PluginAnalyzer`, `RecordScanner`, `SettingsScanner`, `SuspectScanner`
- **New Analyzers**: `FileIntegrityAnalyzer`, `BuffoutVersionAnalyzer`, `BuffoutVersionAnalyzerV2`
- **FCX Analyzers**: `VersionAnalyzer`, `ModConflictAnalyzer` in `Scanner111.Core/FCX/`
- **Execution order**: Priority-based with parallel execution support for independent analyzers
- **Location**: `Scanner111.Core/Analyzers/` and `Scanner111.Core/FCX/`
- **Return type**: All analyzers return `Task<AnalysisResult>`, not generic results

### Message Handler Abstraction
- **UI-agnostic communication** via `IMessageHandler`
- **GUI**: `MessageHandler` in `Scanner111.GUI/Services/`
- **CLI**: `CliMessageHandler` in `Scanner111.Core/Infrastructure/`
- **Purpose**: Decouples business logic from UI concerns

## Critical Implementation Requirements

### String Preservation Rules
- **Output format must match Python reference exactly** - see `sample_logs/crash-*-AUTOSCAN.md` files

### File I/O Standards
- **Always UTF-8 encoding** with error handling for corrupted game files
- **Use `async` file operations** throughout - no blocking I/O
- **Pattern**: `File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken)`
- **Console encoding**: Set UTF-8 explicitly in `Program.cs` for Windows compatibility

### Dependency Injection
- **Constructor injection** for all services - no static dependencies
- **`IApplicationSettingsService`** replaces removed `GlobalRegistry`
- **Registration**: Use `.AddTransient<>()`, `.AddSingleton<>()` in startup

## Project Structure

```
Scanner111.Core/          # Business logic library
├── Analyzers/            # IAnalyzer implementations
├── FCX/                  # File Check Xtended - game integrity analyzers
├── Infrastructure/       # Cross-cutting services (23+ services)
├── Models/              # Domain models (CrashLog, ScanResult)
├── Pipeline/            # IScanPipeline and builders
└── Services/            # Update service and utilities

Scanner111.GUI/          # Avalonia MVVM desktop app
├── ViewModels/          # MVVM view models
├── Views/               # AXAML views
└── Services/            # GUI-specific services

Scanner111.CLI/          # Console app with CommandLineParser
├── Commands/            # ICommand implementations (6 commands)
├── Models/              # CLI-specific options
└── Services/            # CLI-specific services

Scanner111.Tests/        # xUnit test project
```

## Development Workflows

### Testing with Sample Data
```bash
# Use sample_logs/ for testing - each .log has expected -AUTOSCAN.md output
dotnet test --filter "ClassName=FormIdAnalyzerTests"

# Verify output matches expected format in sample_logs/*-AUTOSCAN.md files
dotnet run --project Scanner111.CLI -- scan -l "sample_logs/crash-2023-09-15-01-54-49.log"
```

### CommandLineParser Integration
```bash
# CLI uses verb-based commands with CommandLineParser
dotnet run --project Scanner111.CLI -- scan -l "path/to/crash.log" --verbose
dotnet run --project Scanner111.CLI -- fcx -g "C:\Games\Fallout 4" --check-integrity  # FCX mode
dotnet run --project Scanner111.CLI -- demo  # Shows sample analysis
dotnet run --project Scanner111.CLI -- config  # Manages settings
dotnet run --project Scanner111.CLI -- about   # Version info
```

### Building and Running
```bash
# Build solution
dotnet build

# Run GUI (Avalonia, no DI container in App.axaml.cs yet)
dotnet run --project Scanner111.GUI

# Run CLI with dependency injection via ServiceCollection
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
- **FCX Mode**: File integrity checking via `HashValidationService` and `IBackupService`
- **Game integrity**: FCX analyzers validate game files, scripts, and mod compatibility

### Performance Monitoring
- **Decorator pattern**: `PerformanceMonitoringPipeline` wraps base pipeline
- **Metrics collection**: Track per-file and batch processing times
- **Logging**: Use `ILogger` abstraction, structured logging with timing data

### FCX (File Check Xtended) Infrastructure
- **Hash validation**: `IHashValidationService` with concurrent caching and progress reporting
- **Backup management**: `IBackupService` for game file backup/restore operations
- **Mod compatibility**: `IModCompatibilityService` for version checking and conflict detection
- **Game integrity**: FCX analyzers validate files against known hashes and detect corruption
- **Performance**: Uses buffered I/O (1MB buffers) and async operations for large file processing

## Reference Implementation
- **Python source**: `Code to Port/` directory contains original implementation
- **Expected outputs**: `sample_logs/` contains crash logs with expected `-AUTOSCAN.md` outputs
- **Sample logs**: Use for testing - outputs must match exactly
- **YAML databases**: `Data/CLASSIC Main.yaml` and `Data/CLASSIC Fallout4.yaml` contain lookup tables

## Testing Patterns
- **TestHelpers/TestImplementations.cs**: Mock services for unit tests
- **xUnit framework**: All test projects use xUnit with proper async patterns
- **Integration tests**: `Scanner111.Tests/Integration/` for end-to-end scenarios
- **Test data**: Use `sample_logs/` files to verify analyzer output format exactly matches expected
- **Resource management**: All tests use `using` statements and proper disposal patterns

## UI Patterns

### MVVM (GUI)
- **ViewModels**: Inherit from base classes with `INotifyPropertyChanged`
- **Commands**: Use `ReactiveCommand` for async operations
- **Theme**: Dark theme with `#2d2d30` background, `#0e639c` primary color

### CLI Commands
- **CommandLineParser**: Use verbs pattern (`scan`, `demo`, `config`, `about`, `fcx`)
- **Options classes**: In `Scanner111.CLI/Models/` with proper attributes
- **FCX Command**: `FcxCommand` for game integrity checking with hash validation
- **Async execution**: All commands implement `ICommand` with async execution
- **DI setup**: CLI uses `ServiceCollection` in `Program.cs` with proper service registration
