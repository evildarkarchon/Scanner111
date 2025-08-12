# GitHub Copilot Instructions for Scanner111

## Project Overview
Scanner111 is a C# port of a Python crash log analyzer for Bethesda games (Fallout 4, Skyrim). It analyzes crash dumps to identify problematic game modifications using multiple analyzer engines in a streaming pipeline architecture. Features both GUI (Avalonia) and CLI interfaces with rich Terminal UI using Spectre.Console.

## Core Architecture Patterns

### Async Streaming Pipeline (Critical)
- **All operations use `IAsyncEnumerable<T>`** for streaming results, never collect to lists
- **Producer/consumer with `System.Threading.Channels`** for decoupled async processing
- **`IScanPipeline` interface** at `Scanner111.Core/Pipeline/` - decorator pattern with performance monitoring
- **Pipeline decorators**: `PerformanceMonitoringPipeline`, `FcxEnabledPipeline`, `EnhancedScanPipeline`

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
- **Enhanced Analyzers**: `FileIntegrityAnalyzer`, `BuffoutVersionAnalyzerV2`
- **FCX Analyzers**: File integrity checking in `Scanner111.Core/FCX/`
- **Execution order**: Priority-based (lower = first), parallel execution where `CanRunInParallel = true`
- **Return type**: All analyzers return `Task<AnalysisResult>`, not generic results

### Message Handler Abstraction
- **UI-agnostic communication** via `IMessageHandler` at `Scanner111.Core/Infrastructure/MessageHandler.cs`
- **GUI**: `GuiMessageHandlerService` in `Scanner111.GUI/Services/`
- **CLI**: `SpectreMessageHandler` and `EnhancedSpectreMessageHandler` in `Scanner111.CLI/Services/`
- **Static facade**: `MessageHandler` class provides `MsgInfo()`, `MsgError()`, etc. shortcuts
- **Legacy support**: `--legacy-progress` flag switches to basic CLI message handler
- **Purpose**: Decouples business logic from UI concerns, enables rich terminal UI via Spectre.Console

### Terminal UI Architecture (Spectre.Console)
- **Interactive Mode**: CLI launches TUI when no arguments provided (`Environment.UserInteractive`)
- **`ITerminalUIService`**: Interface for terminal UI operations at `Scanner111.CLI/Services/`
- **`SpectreTerminalUIService`**: Full TUI implementation with menus, progress bars, live updates
- **Capabilities check**: Uses `AnsiConsole.Profile.Capabilities.Interactive` to detect terminal support
- **Rich components**: FigletText headers, selection prompts, progress contexts, live panels

### Dependency Injection Architecture
- **Constructor injection** for all services - no static dependencies except `MessageHandler` facade
- **`IApplicationSettingsService`** for JSON settings persistence
- **`IYamlSettingsProvider`** for game-specific YAML data access
- **Service lifetimes**: Core services are Singletons, Commands are Transient
- **Registration**: CLI uses `ServiceCollection` in `Program.cs`, GUI in `App.axaml.cs`
- **Key Services**: `IUpdateService`, `ICacheManager`, `IUnsolvedLogsMover`, `IModManagerDetector`
- **Message Handler Selection**: `--legacy-progress` flag controls handler registration at startup

## Project Structure

```
Scanner111.Core/          # Business logic library
├── Analyzers/            # IAnalyzer implementations
├── FCX/                  # File Check Xtended - game integrity analyzers
├── Infrastructure/       # Cross-cutting services (23+ services)
├── Models/              # Domain models (CrashLog, ScanResult)
├── ModManagers/         # MO2/Vortex integration services
├── Pipeline/            # IScanPipeline and builders
└── Services/            # Update service and utilities

Scanner111.GUI/          # Avalonia MVVM desktop app
├── ViewModels/          # MVVM view models
├── Views/               # AXAML views
└── Services/            # GUI-specific services

Scanner111.CLI/          # Console app with CommandLineParser
├── Commands/            # ICommand implementations (8 commands: scan, watch, demo, config, about, fcx, interactive)
├── Models/              # CLI-specific options
└── Services/            # CLI-specific services

Scanner111.Tests/        # xUnit test project
```

## Critical Implementation Requirements

### Output Format Preservation
- **Output format must match Python reference exactly** - verify against `sample_logs/crash-*-AUTOSCAN.md` files
- **Use exact string formatting** - no deviation from expected format

### File I/O Standards
- **Always UTF-8 encoding** with error handling: `File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken)`
- **All operations must be async** with proper cancellation support
- **Console encoding**: Set UTF-8 explicitly in CLI `Program.cs` for Windows compatibility
- **Resource management**: Use `using` statements and `IAsyncDisposable` where appropriate

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
dotnet run --project Scanner111.CLI -- watch -p "C:/path/to/logs" --auto-move --dashboard  # Monitor directory
dotnet run --project Scanner111.CLI -- fcx -g "C:\Games\Fallout 4" --check-integrity  # FCX mode
dotnet run --project Scanner111.CLI -- demo  # Shows sample analysis
dotnet run --project Scanner111.CLI -- config  # Manages settings
dotnet run --project Scanner111.CLI -- about   # Version info
dotnet run --project Scanner111.CLI -- interactive  # Explicit TUI mode

# Interactive mode (auto-launches when no args provided in terminal)
dotnet run --project Scanner111.CLI
```

### Building and Running
```bash
# Build solution
dotnet build

# Run GUI (Avalonia with DI container in App.axaml.cs)
dotnet run --project Scanner111.GUI

# Run CLI with dependency injection via ServiceCollection
dotnet run --project Scanner111.CLI -- scan -l "path/to/crash.log" --verbose

# Interactive Terminal UI (launches when no arguments provided)
dotnet run --project Scanner111.CLI
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
- **Mod Manager Support**: `ModManagerDetector` and `ModManagerService` for MO2/Vortex integration
- **Update Service**: Automatic version checking with configurable startup checks

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
- **Component tests**: Separate folders for Analyzers/, CLI/, FCX/, GUI/, ModManagers/
- **Test data**: Use `sample_logs/` files to verify analyzer output format exactly matches expected
- **Resource management**: All tests use `using` statements and proper disposal patterns
- **Mock services**: Use `TestApplicationSettingsService`, `TestMessageHandler`, etc. from TestHelpers
- **DI in tests**: Set up `ServiceCollection` with test implementations for analyzer tests

## UI Patterns

### MVVM (GUI)
- **ViewModels**: Inherit from base classes with `INotifyPropertyChanged`
- **Commands**: Use `ReactiveCommand` for async operations
- **Theme**: Dark theme with `#2d2d30` background, `#0e639c` primary color

### CLI Commands
- **CommandLineParser**: Use verbs pattern (`scan`, `watch`, `demo`, `config`, `about`, `fcx`, `interactive`)
- **Options classes**: In `Scanner111.CLI/Models/` with proper attributes
- **Watch Command**: `WatchCommand` for real-time directory monitoring with FileSystemWatcher
- **FCX Command**: `FcxCommand` for game integrity checking with hash validation
- **Interactive Command**: `InteractiveCommand` launches full TUI via `ITerminalUIService`
- **Async execution**: All commands implement `ICommand` with async execution
- **DI setup**: CLI uses `ServiceCollection` in `Program.cs` with proper service registration
- **Message handlers**: `--legacy-progress` flag switches between enhanced and basic CLI output

### Terminal UI (TUI) Patterns
- **Auto-detection**: Interactive mode launches when `args.Length == 0 && Environment.UserInteractive`
- **Capability checking**: Use `AnsiConsole.Profile.Capabilities.Interactive` before TUI operations
- **Menu system**: `SelectionPrompt<string>` with styled choices and navigation
- **Live displays**: Combine `Layout`, `Table`, and `Live` for real-time updates
- **Progress contexts**: Multi-progress tracking with concurrent operations
- **Rich formatting**: FigletText headers, markup styling, panels with borders

```csharp
// TUI capability check pattern
if (!AnsiConsole.Profile.Capabilities.Interactive)
{
    AnsiConsole.MarkupLine("[red]Error:[/] Interactive mode requires an interactive terminal.");
    return 1;
}
```
