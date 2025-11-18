# CLAUDE.md

This file provides guidance to Claude Code when working with the Scanner111 project.

## Project Overview

**Scanner111** is a C# port of CLASSIC (Crash Log Auto Scanner & Setup Integrity Checker), a tool for analyzing crash logs from Bethesda games (Fallout 4 and Skyrim). The original CLASSIC is a hybrid Python-Rust application; Scanner111 aims to achieve feature parity using modern C# and the Avalonia UI framework.

**Status**: Foundation phase - no production code written yet. Currently porting from the hybrid Rust/Python codebase in `Code_To_Port/`.

**Goals**:
- Port ~250 crash log analysis checks from Python/Rust to C#
- Create a modern MVVM GUI using Avalonia
- Separate business logic (Scanner111.Common) from UI (Scanner111)
- Comprehensive test coverage with xUnit
- Cross-platform support via Avalonia (.NET 9.0)

## Quick Start

### Prerequisites
- .NET 9.0 SDK or later
- Visual Studio 2022 or JetBrains Rider (recommended IDEs)
- Git for source control

### Building the Project
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run the GUI application
dotnet run --project Scanner111/Scanner111.csproj
```

**IMPORTANT**: Never use `--no-build` with `dotnet run` or `dotnet test`. This would ignore changes that might have fixed issues.

## Architecture

### Project Structure

```
Scanner111/                      # Solution root
├── Scanner111/                  # Avalonia MVVM GUI application
│   ├── Views/                  # Avalonia XAML views
│   ├── ViewModels/             # ViewModels for MVVM pattern
│   ├── Models/                 # UI-specific models
│   ├── Assets/                 # Images, icons, resources
│   └── Scanner111.csproj       # GUI project (WinExe, net9.0-windows)
├── Scanner111.Common/           # Business logic library
│   ├── LogParsing/            # Crash log parsing components
│   ├── Analysis/              # Log analysis and checks
│   ├── Models/                # Domain models
│   ├── Services/              # Business services
│   └── Scanner111.Common.csproj # Business logic (net9.0)
├── Scanner111.Tests/            # GUI tests (xUnit)
│   ├── ViewModels/            # ViewModel tests
│   ├── Views/                 # View tests (Avalonia.Headless)
│   └── Scanner111.Tests.csproj
├── Scanner111.Common.Tests/     # Business logic tests (xUnit)
│   ├── LogParsing/            # Parser tests
│   ├── Analysis/              # Analysis tests
│   └── Scanner111.Common.Tests.csproj
├── Code_To_Port/                # READ-ONLY: Original Python/Rust source
├── sample_logs/                 # READ-ONLY: 1,312 crash logs for testing
│   ├── FO4/                   # 1,013 Fallout 4 logs
│   └── Skyrim/                # 299 Skyrim logs
└── sample_output/               # Example output from original tool
```

### Technology Stack
- **.NET 9.0**: Target framework
- **C# 13**: Language version (implicit usings, nullable reference types)
- **Avalonia 11.3.8**: Cross-platform UI framework
- **ReactiveUI**: MVVM framework for Avalonia
- **xUnit**: Testing framework
- **FluentAssertions**: Assertion library (recommended)

### Design Principles
1. **Separation of Concerns**: UI (Scanner111) and business logic (Scanner111.Common) are strictly separated
2. **MVVM Pattern**: All UI uses ViewModels, no code-behind logic
3. **Testability**: Business logic is framework-agnostic and fully testable
4. **Async-First**: Use async/await for I/O operations (file reading, parsing)
5. **Immutability**: Prefer immutable data structures where practical

## Development Guidelines

### C# Code Standards

#### File Organization
- **One class per file** (exceptions: small related helpers like DTOs)
- **Namespace matches folder structure** (e.g., `Scanner111.Common.LogParsing`)
- **File name matches primary type** (e.g., `LogParser.cs` contains `LogParser` class)

#### Code Quality
- **Nullable reference types enabled**: All projects use `<Nullable>enable</Nullable>`
- **Complete XML documentation**: All public APIs require `///` XML comments
- **Avoid excessive complexity**: Max 10-12 branches per method (extract methods or use pattern matching)
- **Use modern C# features**: Records, pattern matching, switch expressions, file-scoped namespaces

#### Async Best Practices
- **Async all the way**: Don't mix sync and async code (no `.Result` or `.Wait()`)
- **ConfigureAwait(false)**: Use in library code (Scanner111.Common) to avoid context capture
- **CancellationToken**: Accept `CancellationToken` for long-running async operations
- **ValueTask**: Consider `ValueTask<T>` for hot paths and frequently called methods

### Avalonia MVVM Patterns

#### ViewModels
- Inherit from `ReactiveObject` or `ViewModelBase`
- Use `ReactiveUI` properties with `RaiseAndSetIfChanged()`
- Expose `ReactiveCommand` for user actions
- No direct file I/O or heavy computation (delegate to services)
- ViewModels should be testable without Avalonia runtime

#### Data Binding
- **Compiled bindings by default**: `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`
- Strongly-typed bindings in XAML: `{Binding Property, DataType=vm:MyViewModel}`
- Avoid code-behind event handlers (use `ReactiveCommand` instead)

#### View Testing
- Use **Avalonia.Headless** for UI tests
- Focus tests on ViewModels (easier to test than Views)
- Integration tests should verify view-viewmodel bindings

### File I/O and Crash Log Handling

#### Encoding
- **UTF-8 with error handling**: Use `Encoding.UTF8` with error handling for crash logs
- **Handle incomplete logs**: Sample logs include partial files (crash logger couldn't write complete file)
- **Robust parsing**: Expect malformed input, missing sections, encoding issues

#### Path Handling
- **Use Path.Combine()**: Never concatenate path strings manually
- **Cross-platform paths**: Use `Path.DirectorySeparatorChar` or `Path.Combine()`
- **Validate paths**: Check `File.Exists()` / `Directory.Exists()` before operations

#### Sample Logs for Testing
- **1,013 Fallout 4 logs** in `sample_logs/FO4/`
- **299 Skyrim logs** in `sample_logs/Skyrim/`
- **Diverse content**: Full logs, partial logs, various crash types
- **READ-ONLY**: Never modify files in `sample_logs/`

### Testing Standards

#### Test Organization
- **Domain-driven structure**: Mirror source structure (e.g., `LogParsing/` tests in `LogParsing/`)
- **File naming**: `<ClassName>Tests.cs` (e.g., `LogParserTests.cs`)
- **Test method naming**: `MethodName_Scenario_ExpectedResult` (e.g., `ParseLog_WhenLogIsIncomplete_ReturnsPartialResult`)

#### xUnit Best Practices
- **Use `[Fact]` for simple tests**, `[Theory]` with `[InlineData]` for parameterized tests
- **Use `IAsyncLifetime`** for async setup/teardown
- **Dispose resources properly**: Implement `IDisposable` or use `using` statements
- **Isolate tests**: No shared state between tests (use `IClassFixture<T>` for controlled sharing)

#### Test Coverage Goals
- **Business logic (Scanner111.Common)**: Aim for 80%+ coverage
- **ViewModels**: Test all commands, property changes, and business logic
- **Views**: Integration tests for critical user flows only

#### Testing Anti-Patterns
- ❌ Modifying production files in tests → ✅ Use test-specific files or in-memory data
- ❌ Testing implementation details → ✅ Test public API behavior
- ❌ Tests depending on execution order → ✅ Independent, isolated tests
- ❌ Mocking everything → ✅ Use real objects where practical (especially value types)

### Common Anti-Patterns to Avoid

#### General
- ❌ String concatenation for paths → ✅ `Path.Combine()`
- ❌ `async void` (except event handlers) → ✅ `async Task`
- ❌ `.Result` or `.Wait()` on async code → ✅ `await`
- ❌ Swallowing exceptions → ✅ Log and rethrow or handle appropriately
- ❌ Missing XML documentation → ✅ Complete `///` comments

#### Avalonia-Specific
- ❌ Code-behind logic → ✅ ViewModels and commands
- ❌ UI thread blocking → ✅ Async operations with progress indicators
- ❌ Direct property assignment → ✅ `RaiseAndSetIfChanged()`
- ❌ Tightly coupled ViewModels → ✅ Dependency injection and interfaces

#### Windows Development
- ❌ Writing to `nul` or `NUL` → ✅ Use `Stream.Null` or in-memory streams
- ❌ Platform-specific APIs without checks → ✅ Use `OperatingSystem.IsWindows()` guards
- ❌ Hardcoded backslashes in paths → ✅ `Path.Combine()` or `Path.DirectorySeparatorChar`

## Reference Materials

### Code_To_Port (READ-ONLY)
The `Code_To_Port/` directory contains the original CLASSIC implementation as a git submodule. This is reference material for understanding the original functionality.

**Important Rules**:
1. **READ-ONLY**: Never modify files in `Code_To_Port/`
2. **Reference only**: Use to understand algorithms, checks, and business logic
3. **Will be removed**: Once feature parity is achieved, this directory will be deleted
4. **Check CLASSIC's CLAUDE.md**: `Code_To_Port/CLAUDE.md` has extensive documentation

**Key Files to Reference**:
- `Code_To_Port/README.md`: Project overview
- `Code_To_Port/CLAUDE.md`: Detailed development guide (Python/Rust patterns)
- `Code_To_Port/src/classic/`: Python business logic
- `Code_To_Port/ClassicLib/`: Core library components
- `Code_To_Port/rust/`: Rust acceleration modules

### Sample Logs (READ-ONLY)
The `sample_logs/` directory contains 1,312 real crash logs from Bethesda games:
- **1,013 Fallout 4 logs** (`sample_logs/FO4/`)
- **299 Skyrim logs** (`sample_logs/Skyrim/`)

**Important Rules**:
1. **READ-ONLY**: Never modify files in `sample_logs/`
2. **Use for testing**: Perfect for unit tests, integration tests, and manual verification
3. **Diverse content**: Includes full logs, partial logs, various crash types
4. **Encoding issues**: Some logs may have encoding problems (test robustness)

**Testing with Sample Logs**:
```csharp
[Theory]
[InlineData("sample_logs/FO4/crash-example.log")]
[InlineData("sample_logs/Skyrim/crash-2023-12-07-02-24-27.log")]
public async Task ParseLog_WithRealLogs_Succeeds(string logPath)
{
    // Arrange
    var parser = new LogParser();

    // Act
    var result = await parser.ParseAsync(logPath);

    // Assert
    result.Should().NotBeNull();
}
```

## Important Notes

### Windows Development Warnings
- **Never output to `nul` or `NUL`**: On Windows, this creates an undeletable file on system drives. Use `Stream.Null` instead.
- **Git Bash compatibility**: When using automation scripts, be aware of Windows path separators and shell differences.
- **Test in Windows environment**: Since the target platform is Windows (`net9.0-windows10.0.19041.1`), ensure tests pass on Windows.

### .NET 9.0 Specific
- **Implicit usings enabled except in Scanner111.csproj**: Common namespaces are automatically imported (except in Scanner111.csproj)
- **Nullable reference types enabled**: All projects enforce null safety
- **Top-level statements**: Entry point can use top-level statements (Program.cs in Scanner111)
- **File-scoped namespaces**: Prefer `namespace Scanner111.Common.LogParsing;` over braces

### Dependencies
- **Avalonia 11.3.8**: Keep all Avalonia packages at the same version
- **ReactiveUI**: Part of Avalonia.ReactiveUI package
- **Diagnostics**: `Avalonia.Diagnostics` only included in Debug builds (see csproj conditional)

### API Documentation
- **XML comments required**: All public types, methods, properties require `///` documentation
- **Generate documentation**: Consider enabling `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
- **Document exceptions**: Use `<exception cref="T">` tags for thrown exceptions

## Memories

### Project-Specific
- **Code_To_Port and sample_logs are READ-ONLY**: Never modify these directories
- **Code_To_Port will be removed**: Once feature parity is achieved
- **Sample logs are diverse**: Some full, some partial due to crash logger failures
- **Scanner111 is GUI-only**: Business logic goes in Scanner111.Common
- **xUnit for all tests**: Both projects use xUnit as the testing framework

### From Global CLAUDE.md
- **Never use `--no-build`**: Always run `dotnet run` and `dotnet test` with full builds
- **Never write to `nul`/`NUL`**: Creates undeletable files on Windows system drives
- **When fixing tests**: Focus on fixing the underlying issue, not just making the test pass
- **Large modifications**: Delegate to subagents rather than using `sed` or automation scripts
- **No project milestone tracking unless relevant to future development**: Don't use CLAUDE.md for tracking milestones, unless it is relevant for future development.

## Development Workflow

### Starting a New Feature
1. **Understand the original**: Review corresponding code in `Code_To_Port/`
2. **Design the C# API**: Plan classes, interfaces, and public methods
3. **Write tests first**: TDD approach for business logic
4. **Implement incrementally**: Small, testable changes
5. **Test with sample logs**: Verify against real crash logs
6. **Document thoroughly**: XML comments for all public APIs

### Porting Checklist
When porting a feature from `Code_To_Port/`:
- [ ] Identify Python/Rust source files
- [ ] Understand the algorithm and business logic
- [ ] Design equivalent C# types and methods
- [ ] Write tests using sample logs
- [ ] Implement in Scanner111.Common (if business logic)
- [ ] Create ViewModel and View (if GUI component)
- [ ] Verify against original behavior with sample logs
- [ ] Document deviations or improvements

### Code Review Guidelines
- Verify separation of concerns (UI vs business logic)
- Check for async/await best practices
- Ensure XML documentation is complete
- Verify tests are isolated and deterministic
- Check that sample_logs and Code_To_Port remain unmodified
- Validate MVVM pattern adherence (no code-behind logic)

## Additional Resources

### Avalonia Documentation
- [Avalonia Docs](https://docs.avaloniaui.net/)
- [ReactiveUI Docs](https://www.reactiveui.net/docs/)
- [Avalonia MVVM Tutorial](https://docs.avaloniaui.net/docs/tutorials/todo-list-app)

### .NET Documentation
- [.NET 9 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/)
- [C# 13 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)
- [Async/Await Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

### Testing Resources
- [xUnit Documentation](https://xunit.net/docs/getting-started/netfx/visual-studio)
- [FluentAssertions](https://fluentassertions.com/)
- [Avalonia.Headless](https://docs.avaloniaui.net/docs/guides/building-cross-platform-applications/headless-testing)
