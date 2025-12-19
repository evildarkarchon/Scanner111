<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

## Project Overview

**Scanner111** is a C# port of CLASSIC (Crash Log Auto Scanner & Setup Integrity Checker), a tool for analyzing crash logs from Bethesda games (Fallout 4). Built with .NET 9.0 and Avalonia UI.

## Build and Test Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run single test file
dotnet test --filter "FullyQualifiedName~LogParserTests"

# Run single test method
dotnet test --filter "FullyQualifiedName~LogParserTests.ParseLog_WithValidLog_ReturnsResult"

# Run tests in specific project
dotnet test Scanner111.Common.Tests/Scanner111.Common.Tests.csproj

# Run GUI application
dotnet run --project Scanner111/Scanner111.csproj

# Run benchmarks (Release mode required)
dotnet run -c Release --project Scanner111.Benchmarks/Scanner111.Benchmarks.csproj
```

**IMPORTANT**: Never use `--no-build` with `dotnet run` or `dotnet test`.

## Architecture

### Projects

| Project                     | Target                     | Purpose                                         |
| --------------------------- | -------------------------- | ----------------------------------------------- |
| **Scanner111**              | net9.0-windows10.0.19041.0 | Avalonia MVVM GUI                               |
| **Scanner111.Common**       | net9.0                     | Business logic (parsing, analysis, reporting)   |
| **Scanner111.Tests**        | net9.0-windows             | GUI/ViewModel tests (xUnit + Avalonia.Headless) |
| **Scanner111.Common.Tests** | net9.0                     | Business logic tests (xUnit + FluentAssertions) |
| **Scanner111.Benchmarks**   | net10.0                    | Performance benchmarks (BenchmarkDotNet)        |

### Key Service Layers (Scanner111.Common)

```
Services/
├── Parsing/         # ILogParser, CrashHeaderParser - log file parsing
├── Analysis/        # IPluginAnalyzer, ISuspectScanner, IFormIdAnalyzer - crash analysis
├── Configuration/   # IYamlConfigLoader, IConfigurationCache - YAML config handling
├── Reporting/       # IReportBuilder, IReportWriter - markdown report generation
├── Orchestration/   # ILogOrchestrator, IScanExecutor - analysis pipeline coordination
├── Database/        # IDatabaseConnectionFactory - SQLite FormID lookups
└── FileIO/          # IFileIOService - file system abstraction
```

### Dependency Injection

Services are registered in [App.axaml.cs](Scanner111/App.axaml.cs) using `Microsoft.Extensions.DependencyInjection`:
- **Singletons**: File I/O, parsers, analyzers, configuration services
- **Transient**: ViewModels, LogOrchestrator (per-scan instance)
- **Factory delegates**: `Func<TViewModel>` for navigation

### MVVM Pattern

- ViewModels inherit `ViewModelBase` (which inherits `ReactiveObject`)
- Use `ReactiveCommand` for user actions
- Shared state via `IScanResultsService` and `ISettingsService` singletons

## Development Guidelines

### Async Patterns
- Use `ConfigureAwait(false)` in Scanner111.Common library code
- Accept `CancellationToken` for long-running operations
- Never use `.Result` or `.Wait()` - async all the way

### Testing
- Test method naming: `MethodName_Scenario_ExpectedResult`
- Use `[Fact]` for simple tests, `[Theory]` with `[InlineData]` for parameterized
- Tests use NSubstitute and Moq for mocking, FluentAssertions for assertions

### Windows-Specific
- **Never write to `nul` or `NUL`** - creates undeletable files on Windows. Use `Stream.Null` instead
- Always use `Path.Combine()` for path construction

## Reference Directories (READ-ONLY)

### Code_To_Port/
Original CLASSIC Python/Rust implementation (git submodule). Reference for porting logic, will be removed at feature parity.
- `Code_To_Port/CLAUDE.md`: Detailed documentation of original architecture
- `Code_To_Port/ClassicLib/`: Python business logic
- `Code_To_Port/rust/`: Rust acceleration modules

### sample_logs/
1,312 real crash logs for testing (1,013 FO4, 299 Skyrim). Includes full logs, partial logs, and various encoding edge cases.