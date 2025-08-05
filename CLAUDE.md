# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Scanner111 is a C# port of a Python crash log analyzer for Bethesda games (Fallout 4, Skyrim, etc.). The application analyzes crash logs to identify problematic game modifications. It provides both GUI (Avalonia) and CLI interfaces.

## Key Commands

### Build & Run
```bash
# Build solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Run GUI application
dotnet run --project Scanner111.GUI

# Run CLI application (default scan verb)
dotnet run --project Scanner111.CLI -- scan

# Run CLI with specific log file
dotnet run --project Scanner111.CLI -- scan -l "path/to/crash.log"

# Run CLI demo mode
dotnet run --project Scanner111.CLI -- demo

# Run CLI config command
dotnet run --project Scanner111.CLI -- config

# Run CLI about command  
dotnet run --project Scanner111.CLI -- about

# Run CLI FCX command (enhanced file checks)
dotnet run --project Scanner111.CLI -- fcx

# Run all tests
dotnet test

# Run tests with detailed output
dotnet test -v normal

# Run specific test by name
dotnet test --filter "FullyQualifiedName~TestName"

# Run tests for specific class
dotnet test --filter "ClassName=FormIdAnalyzerTests"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## High-Level Architecture

### Async Streaming Pipeline Pattern
The core of the application uses `IAsyncEnumerable<T>` for streaming results without memory accumulation:
- **IScanPipeline**: Orchestrates analysis with batch processing capabilities
- **Producer/Consumer**: Uses `System.Threading.Channels` for decoupled async processing
- **Decorator Pattern**: `PerformanceMonitoringPipeline` wraps base pipeline for metrics
- All I/O operations use async patterns with `ConfigureAwait(false)`
- SemaphoreSlim guards concurrent file access

### Analyzer Factory Pattern
- **IAnalyzer Interface**: All analyzers implement with Priority, CanRunInParallel, Name properties
- **Priority Execution**: Lower priority values run first, parallel execution for independent analyzers
- **Core Analyzers**: FormIdAnalyzer, PluginAnalyzer, RecordScanner, SettingsScanner, SuspectScanner, BuffoutVersionAnalyzer
- **FCX Analyzers**: FileIntegrityAnalyzer, ModConflictAnalyzer, VersionAnalyzer
- All analyzers return `Task<AnalysisResult>`, not generic results

### Message Handler Abstraction
UI-agnostic communication through `IMessageHandler`:
- **GUI Implementation**: `MessageHandler` in Scanner111.GUI/Services/
- **CLI Implementation**: `CliMessageHandler` in Scanner111.Core/Infrastructure/
- Decouples business logic from presentation concerns

### Dependency Injection Architecture
- Constructor injection for all services (no static dependencies)
- `IApplicationSettingsService` replaces removed GlobalRegistry
- `IYamlSettingsProvider` for centralized YAML configuration access
- Services registered in CLI Program.cs with proper lifetimes

## Critical Implementation Requirements

### Output Format
- Output format must match Python reference implementation exactly
- Verify against `sample_logs/*-AUTOSCAN.md` expected outputs

### File I/O Standards
- Always use UTF-8 encoding with error handling: `Encoding.UTF8`
- All operations must be async with proper cancellation support
- Console encoding set explicitly in Program.cs for Windows compatibility

### Resource Management
- Use `using` statements for all IDisposable resources
- Implement IAsyncDisposable where appropriate
- Proper cancellation token propagation through all async methods

## Solution Structure

- **Scanner111.Core**: Business logic library with analyzers and pipeline
  - Analyzers/: IAnalyzer implementations
  - Infrastructure/: Cross-cutting services (CrashLogParser, ReportWriter, etc.)
  - Models/: Domain models and YAML configurations
  - Pipeline/: IScanPipeline and decorators
  - FCX/: Enhanced file checking components
  - Services/: Application services

- **Scanner111.GUI**: Avalonia MVVM desktop application
  - ViewModels/: MVVM view models with INotifyPropertyChanged
  - Views/: AXAML views with dark theme (#2d2d30 background, #0e639c primary)
  - Services/: GUI-specific services

- **Scanner111.CLI**: Console application using CommandLineParser
  - Commands/: ICommand implementations (ScanCommand, DemoCommand, etc.)
  - Models/: CLI options classes with CommandLineParser attributes
  - Services/: CLI-specific services

- **Scanner111.Tests**: xUnit test project
  - Integration/: End-to-end test scenarios
  - TestImplementations.cs: Mock services and helpers

## Development Workflow

### Analysis Pipeline Flow
1. **CrashLogParser** reads and parses crash log files (UTF-8, error-tolerant)
2. **IScanPipeline** orchestrates the analysis process:
   - Loads analyzers sorted by priority
   - Executes analyzers in parallel groups where possible
   - Streams results via IAsyncEnumerable (never accumulate)
3. **Analyzers** examine different aspects of the crash
4. **ReportWriter** formats results matching Python output exactly

### Testing with Sample Data
- Use `sample_logs/` directory for testing - each .log has expected `-AUTOSCAN.md` output
- Verify analyzer output format matches expected files exactly
- Test files use xUnit with proper async patterns

### Reference Implementation
- **Python source**: `Code to Port/` directory (read-only reference)
- **YAML databases**: `Data/CLASSIC Main.yaml` and `Data/CLASSIC Fallout4.yaml`
- **Sample logs**: `sample_logs/` with expected outputs

## CLI Options Reference

The CLI supports various options for the scan command:
- `-l, --log`: Path to specific crash log file
- `-d, --scan-dir`: Directory to scan for crash logs
- `-g, --game-path`: Path to game installation directory
- `-v, --verbose`: Enable verbose output
- `--fcx-mode`: Enable FCX mode for enhanced file checks
- `--show-fid-values`: Show FormID values (slower scans)
- `--simplify-logs`: Simplify logs (Warning: May remove important information)
- `--move-unsolved`: Move unsolved logs to separate folder
- `--crash-logs-dir`: Directory to store copied crash logs
- `--skip-xse-copy`: Skip automatic XSE (F4SE/SKSE) crash log copying
- `--disable-progress`: Disable progress bars in CLI mode
- `--disable-colors`: Disable colored output
- `-o, --output-format`: Output format (detailed or summary)

## Async I/O Best Practices

- Use `SemaphoreSlim` for controlling concurrent file access
- Implement thread-safe async read/write operations
- Use `Channel<T>` for managing async I/O streams
- Always pass CancellationToken to async operations
- Use IProgress<T> for long-running operations

## Memories

- The files in `Code to Port` should be considered read only as they are just reference.