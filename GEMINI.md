# Scanner111 - Crash Log Analyzer

## Project Overview

**Scanner111** is a high-performance crash log analyzer for Bethesda games (Fallout 4), currently being ported from a hybrid Python/Rust application ("CLASSIC") to **C# and Avalonia UI**.

This repository contains two distinct applications:
1.  **Scanner111 (Active Development):** The new .NET 9.0 / Avalonia UI application.
2.  **Code_to_Port (Reference):** The original Python/Rust application used as a reference for the port.

## Directory Structure

### Scanner111 (New C# Application)
*   **`Scanner111/`**: The main Avalonia UI application (MVVM architecture).
*   **`Scanner111.Common/`**: Core business logic, parsing, and analysis (NetStandard 2.0 / .NET 9.0).
*   **`Scanner111.Benchmarks/`**: Performance profiling projects using BenchmarkDotNet.
*   **`Scanner111.Tests/`**: Unit and integration tests.
*   **`Scanner111.slnx`**: The Visual Studio solution file.

### Code_to_Port (Legacy/Reference Application)
*   **`Code_to_Port/`**: Contains the source code for the original CLASSIC tool.
    *   **`ClassicLib/`**: Python source code.
    *   **`rust/`**: Rust source code (performance-critical modules).
    *   **`GEMINI.md`**: Detailed documentation for the legacy codebase.

## Development: Scanner111 (C#)

### Prerequisites
*   **.NET 9.0 SDK**
*   Visual Studio 2022 or VS Code with C# Dev Kit.

### Build & Run
```bash
# Build the solution
dotnet build

# Run the UI Application
dotnet run --project Scanner111/Scanner111.csproj

# Run Benchmarks
dotnet run -c Release --project Scanner111.Benchmarks/Scanner111.Benchmarks.csproj
```

### Testing
```bash
# Run all tests
dotnet test
```

### Architecture Notes
*   **UI Framework:** Avalonia UI with MVVM pattern.
*   **Concurrency:** Async-first design. Long-running scans are throttled using `SemaphoreSlim`.
*   **Performance:** Log parsing uses optimized Regex and takes ~150us per log.
*   **Navigation:** Uses a Sidebar + Page navigation model.

## Development: Code_to_Port (Python/Rust Reference)

If you need to run the original application for comparison:

```bash
cd Code_to_Port

# Install dependencies
uv sync --all-extras

# Run the GUI
uv run python CLASSIC_Interface.py
```

*Refer to `Code_to_Port/GEMINI.md` for deep-dive details on the legacy architecture.*

## Porting Status

**Current Status:** Phase 11 Complete (UI Architecture Overhaul).
**Goal:** Reach feature parity with CLASSIC while improving performance and maintainability.

### Key Conventions
*   **Tests:** Maintain high test coverage (currently 213+ tests passing). New features must be tested.
*   **Benchmarks:** Use `Scanner111.Benchmarks` to verify performance gains for critical paths (parsing, analysis).
*   **UI:** Follow Avalonia MVVM best practices.
