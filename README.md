# Scanner111

A high-performance crash log analyzer for Bethesda games (Fallout 4), ported from the Python/Rust-based CLASSIC tool to C# and Avalonia UI.

## Status

**Porting Status:** Phase 10 Complete (Optimization & Polish)
**Tests Passing:** 213/213

## Architecture

*   **Scanner111**: Avalonia UI (MVVM) frontend.
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

## Features implemented

*   **Log Parsing**: Fast regex-based parsing of crash logs.
*   **Analysis**:
    *   Plugin detection and load order analysis.
    *   Suspect detection (error messages, stack traces).
    *   Settings validation (Buffout 4 config).
    *   FormID analysis (SQLite database lookup).
*   **Reporting**: Markdown report generation with immutable fragment composition.
*   **GUI**: Modern Avalonia UI with reactive view models.
*   **Concurrency**: Async-first design with `SemaphoreSlim` throttling for batch scans.

## Performance

*   Log parsing takes approximately **~150 microseconds** per log (0.15ms) on modern hardware.
*   Low memory footprint with optimized regex patterns.

## License

Licensed under the GPL-3.0 license. See [LICENSE](LICENSE) file.