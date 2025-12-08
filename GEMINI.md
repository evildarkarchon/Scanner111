# Scanner111 Project Context

## Project Overview

**Scanner111** is a high-performance crash log analyzer for Bethesda games (Fallout 4), currently being ported from the Python/Rust-based "CLASSIC" tool to **.NET 9.0** and **Avalonia UI**.

*   **Goal:** Replicate and improve upon the functionality of the original CLASSIC tool using modern C# technologies.
*   **Current Status:** Phase 10 (Optimization & Polish) - 213/213 tests passing.
*   **Legacy Codebase:** Located in `Code_to_Port/` (Reference only).

## Architecture

The solution (`Scanner111.slnx`) consists of four main projects:

1.  **Scanner111 (GUI)**
    *   **Framework:** Avalonia UI (MVVM pattern).
    *   **Role:** The main desktop application entry point.
    *   **Key Dirs:** `Views/`, `ViewModels/`, `Services/`.

2.  **Scanner111.Common (Core)**
    *   **Framework:** .NET Standard 2.0 / .NET 9.0.
    *   **Role:** Contains all business logic, log parsing, analysis, and reporting.
    *   **Key Components:** `LogParser`, `PluginAnalyzer`, `SuspectScanner`, `ReportBuilder`.

3.  **Scanner111.Tests (Testing)**
    *   **Framework:** xUnit.
    *   **Role:** Unit and integration tests for the Common library and ViewModels.
    *   **Coverage:** High coverage enforced for core logic.

4.  **Scanner111.Benchmarks (Performance)**
    *   **Framework:** BenchmarkDotNet.
    *   **Role:** Performance profiling to ensure the C# port matches or exceeds the Rust implementation.

## Development Workflow

### Building and Running

*   **Build Solution:**
    ```powershell
    dotnet build
    ```

*   **Run GUI:**
    ```powershell
    dotnet run --project Scanner111/Scanner111.csproj
    ```

*   **Run Tests:**
    ```powershell
    dotnet test
    ```

*   **Run Benchmarks:**
    ```powershell
    dotnet run -c Release --project Scanner111.Benchmarks/Scanner111.Benchmarks.csproj
    ```

### Porting & Reference

The `Code_to_Port/` directory contains the source of truth for the logic being ported.
*   **Do not modify** files in `Code_to_Port/` unless explicitly instructed to fix a reference issue.
*   **Reference:** Use `Code_to_Port/CLASSIC_ScanLogs.py` and `Code_to_Port/ClassicLib/` to understand the original logic.
*   **Roadmap:** Refer to `PORTING_ROADMAP.md` for detailed task breakdowns and phase status.

## Key Conventions

*   **Async/Await:** All I/O and heavy computation must be async.
*   **Immutability:** Use `record` types for data models (`ScanConfig`, `LogAnalysisResult`, etc.).
*   **ReactiveUI:** The GUI uses ReactiveUI for MVVM (ReactiveCommand, [Reactive] properties).
*   **Testing:** New features **must** have accompanying unit tests in `Scanner111.Tests`.

## Useful Commands

*   **List all tests:** `dotnet test --list-tests`
*   **Run specific test:** `dotnet test --filter "FullyQualifiedName~LogParserTests"`
