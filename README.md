# Scanner111

A high-performance crash log analyzer for Bethesda games (Fallout 4), ported from the Python/Rust-based CLASSIC tool to C# and Avalonia UI.

## Status

**Porting Status:** Phase 11 Complete (UI Architecture Overhaul)
**Tests Passing:** 213+

## Architecture

*   **Scanner111**: Avalonia UI (MVVM) frontend with sidebar navigation.
*   **Scanner111.Common**: Core business logic, analysis, and reporting (NetStandard 2.0 / Net9.0).
*   **Scanner111.Benchmarks**: Performance profiling using BenchmarkDotNet.
*   **Scanner111.Tests**: Unit and integration tests.

## Getting Started

### Prerequisites

*   .NET 9.0 SDK
*   Visual Studio 2022 or VS Code with C# Dev Kit

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running Benchmarks

```bash
dotnet run -c Release --project Scanner111.Benchmarks/Scanner111.Benchmarks.csproj
```

### Running the Application

```bash
dotnet run --project Scanner111/Scanner111.csproj
```

## Features Implemented

### Core Analysis
*   **Log Parsing**: Fast regex-based parsing of crash logs.
*   **Plugin Analysis**: Plugin detection and load order analysis.
*   **Suspect Detection**: Error messages and stack trace pattern matching.
*   **Settings Validation**: Buffout 4 config checking.
*   **FormID Analysis**: SQLite database lookup.
*   **Reporting**: Markdown report generation with immutable fragment composition.

### GUI (New in Phase 11)
*   **Sidebar Navigation**: Dark-themed sidebar with page navigation.
*   **Dashboard (Home)**: Scan actions, folder browsing, Pastebin fetch.
*   **Results View**: Markdown-rendered AUTOSCAN reports with master-detail layout.
*   **Settings View**: Persistent settings with FCX mode and FormID toggles.
*   **Shared Services**: Cross-ViewModel state management for results and settings.
*   **Concurrency**: Async-first design with `SemaphoreSlim` throttling for batch scans.

## Performance

*   Log parsing takes approximately **~150 microseconds** per log (0.15ms) on modern hardware.
*   Low memory footprint with optimized regex patterns.

## License

Licensed under the GPL-3.0 license. See [LICENSE](LICENSE) file.