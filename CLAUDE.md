# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Scanner111 is a C# port of a Python crash log analyzer for Bethesda games (Fallout 4, Skyrim, etc.). The application analyzes crash logs to identify problematic game modifications. It provides both GUI (Avalonia) and CLI interfaces.

## Memories

- The files in `Code to Port` should be considered read only as they are just reference.

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

## Architecture Overview

### Solution Structure
- **Scanner111.Core**: Business logic library with analyzers and pipeline
- **Scanner111.GUI**: Avalonia MVVM desktop application  
- **Scanner111.CLI**: Console application using CommandLineParser
- **Scanner111.Tests**: xUnit test project

### Core Design Patterns

1. **Async Pipeline Pattern**
   - Use `IAsyncEnumerable` for streaming results
   - Producer/consumer pattern with `System.Threading.Channels`
   - All operations must be async with proper cancellation support
   - `IScanPipeline` with batch processing capabilities

2. **Analyzer Factory Pattern**
   - `IAnalyzer` interface with priority-based execution
   - Dynamic analyzer creation via dependency injection
   - Parallel execution support for independent analyzers
   - Analyzers: FormIdAnalyzer, PluginAnalyzer, RecordScanner, SettingsScanner, SuspectScanner

3. **Message Handler Abstraction**
   - `IMessageHandler` for UI-agnostic communication
   - Different implementations for GUI (MessageHandler) and CLI (CliMessageHandler)

4. **Command Pattern (CLI)**
   - `ICommand` interface for CLI commands
   - Commands: ScanCommand, DemoCommand, ConfigCommand, AboutCommand
   - CommandLineParser for argument parsing

### Critical Implementation Requirements

1. **String Preservation**: Keep "CLASSIC" in all internal strings (only change project/namespace names)
2. **Output Format**: Must match Python reference implementation exactly
3. **File Encoding**: Always use UTF-8 with ignore errors for file reading
4. **GUI Theme**: Dark theme with #2d2d30 background, #0e639c primary color

### Key Components to Implement

**Models** (Scanner111.Core/Models/):
- CrashLog, ScanResult
- Plugin, FormId, ModInfo

**Analyzers** (Scanner111.Core/Analyzers/):
- IAnalyzer interface
- FormIdAnalyzer, PluginAnalyzer, StackAnalyzer, etc.

**Pipeline** (Scanner111.Core/Pipeline/):
- IScanPipeline, ScanPipelineBuilder
- PerformanceMonitor for metrics

**Infrastructure** (Scanner111.Core/Infrastructure/):
- YamlSettingsCache for YAML file caching
- MessageHandler/CliMessageHandler for UI communication
- ApplicationSettingsService for settings management (replaces GlobalRegistry)
- CrashLogParser for parsing crash logs
- FormIdDatabaseService for FormID lookups
- GamePathDetection for auto-detecting game installations
- ReportWriter for generating analysis reports

### Reference Resources

- **Python Implementation**: `Code to Port/` directory
- **Sample Logs**: `sample_logs/` with expected outputs (AUTOSCAN.md files)
- **YAML Data**: `Data/` for lookup data
- **Detailed Guide**: `docs/classic-csharp-ai-implementation-guide.md` for implementation phases

### Development Workflow

1. Check existing Python implementation in `Code to Port/` before implementing any feature
2. Use sample logs in `sample_logs/` to verify output matches expected format
3. Follow MVVM pattern strictly for GUI components
4. Use dependency injection for all services
5. Write unit tests for all analyzers using sample data

### Migration Notes

- GlobalRegistry has been removed - use IApplicationSettingsService instead
- ApplicationSettings is accessed via dependency injection, not static access
- ClassicScanLogsInfo has been removed - use IYamlSettingsProvider to access YAML settings directly
- When creating the .NET port, ensure that the report formatting exactly matches the Python implementation, substituting "CLASSIC" with "Scanner 111" as needed

### Development Best Practices

- **Async Method Best Practices**:
  - Don't forget cancellation tokens in async methods
  - Don't use blocking I/O in async methods

### Resource Management
- Don't forget to dispose resources (use `using` statements)

### Development Reminders
- Always rebuild when testing new changes

### Async I/O Considerations
- Ensure async I/O contention is properly accounted for by:
  - Using `SemaphoreSlim` for controlling concurrent file access
  - Implementing thread-safe async read/write operations
  - Using `Channel<T>` for managing async I/O streams
  - Carefully managing shared resources in multi-threaded async scenarios

### Testing Guidelines

- **Unit Testing**:
  - Any new code must have proper unit tests written or updated after the code is in a state where it compiles properly
  - Test projects use xUnit framework
  - Use TestImplementations.cs for test helpers and mock implementations
  - Integration tests in Scanner111.Tests/Integration/
  - Analyzer tests should verify output format matches expected AUTOSCAN.md files

### CLI Options

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

### Development Guidelines

- **Dependency Injection**: All services use constructor injection
- **Async/Await**: Use async methods throughout, avoid blocking calls
- **Cancellation**: Pass CancellationToken to all async operations
- **Progress Reporting**: Use IProgress<T> for long-running operations
- **File I/O**: Always use UTF-8 encoding with error handling
- **Logging**: Use ILogger abstraction, not console writes

## High-Level Architecture

### Analyzer Pipeline Flow
1. **CrashLogParser** reads and parses crash log files
2. **IScanPipeline** orchestrates the analysis process:
   - Loads analyzers sorted by priority
   - Executes analyzers in parallel groups
   - Streams results via IAsyncEnumerable
3. **Analyzers** examine different aspects:
   - **FormIdAnalyzer**: Detects problematic FormIDs using FormIdDatabaseService
   - **PluginAnalyzer**: Analyzes plugin load order and conflicts
   - **RecordScanner**: Scans for problematic record types
   - **SettingsScanner**: Checks game settings
   - **SuspectScanner**: Identifies known problematic mods
4. **ReportWriter** formats results matching Python output

### Cross-Cutting Concerns
- **IYamlSettingsProvider**: Centralized access to YAML configuration data
- **IApplicationSettingsService**: Application-wide settings management
- **IMessageHandler**: UI-agnostic progress/status reporting
- **GamePathDetection**: Auto-detects game installations from registry/common paths

### Key Implementation Details
- All I/O operations use async patterns with ConfigureAwait(false)
- Channels used for producer/consumer patterns in batch processing
- SemaphoreSlim guards concurrent file access
- Priority-based analyzer execution allows critical checks first