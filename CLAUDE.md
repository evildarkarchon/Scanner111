# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Scanner111 is a C# port of a Python crash log analyzer for Bethesda games (Fallout 4, Skyrim, etc.). The application analyzes crash logs to identify problematic game modifications. It provides both GUI (Avalonia) and CLI interfaces.

## Key Commands

### Build & Run
```bash
# Build solution
dotnet build

# Run GUI application
dotnet run --project Scanner111.GUI

# Run CLI application
dotnet run --project Scanner111.CLI -- scan

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestName"
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

2. **Analyzer Factory Pattern**
   - `IAnalyzer` interface with priority-based execution
   - Dynamic analyzer creation via dependency injection
   - Parallel execution support for independent analyzers

3. **Message Handler Abstraction**
   - `IMessageHandler` for UI-agnostic communication
   - Different implementations for GUI and CLI

### Critical Implementation Requirements

1. **String Preservation**: Keep "CLASSIC" in all internal strings (only change project/namespace names)
2. **Output Format**: Must match Python reference implementation exactly
3. **Class Names**: Keep `ClassicScanLogsInfo` class name (referenced throughout codebase)
4. **File Encoding**: Always use UTF-8 with ignore errors for file reading
5. **GUI Theme**: Dark theme with #2d2d30 background, #0e639c primary color

### Key Components to Implement

**Models** (Scanner111.Core/Models/):
- CrashLog, ScanResult, Configuration
- Plugin, FormId, ModInfo

**Analyzers** (Scanner111.Core/Analyzers/):
- IAnalyzer interface
- FormIdAnalyzer, PluginAnalyzer, StackAnalyzer, etc.

**Pipeline** (Scanner111.Core/Pipeline/):
- IScanPipeline, ScanPipelineBuilder
- PerformanceMonitor for metrics

**Infrastructure** (Scanner111.Core/Infrastructure/):
- YamlSettingsCache for YAML file caching
- MessageHandler for UI communication
- GlobalRegistry for shared state

### Reference Resources

- **Python Implementation**: `Code to Port/` directory
- **Sample Logs**: `sample_logs/` with expected outputs (AUTOSCAN.md files)
- **YAML Data**: `Code to Port/CLASSIC Data/databases/` for configuration
- **Detailed Guide**: `docs/classic-csharp-ai-implementation-guide.md` for implementation phases

### Development Workflow

1. Check existing Python implementation in `Code to Port/` before implementing any feature
2. Use sample logs in `sample_logs/` to verify output matches expected format
3. Follow MVVM pattern strictly for GUI components
4. Use dependency injection for all services
5. Write unit tests for all analyzers using sample data