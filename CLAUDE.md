# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
Scanner111 is a modern C# port of a legacy crash log analysis application. It analyzes game crash logs, identifies problematic plugins, and provides detailed reports. The project prioritizes thread-safety, async patterns, and comprehensive testing.

## Common Development Commands

### Building and Testing
```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run a single test class
dotnet test --filter "Scanner111.Test.Analysis.Analyzers.PluginAnalyzerTests"

# Run a specific test method
dotnet test --filter "AnalyzeAsync_WithCallStackMatches_ReturnsPluginSuspects"

# Build and run CLI application
dotnet run --project Scanner111.CLI

# Build and run Desktop application  
dotnet run --project Scanner111.Desktop
```

### Project Structure Commands
```bash
# Clean build artifacts
dotnet clean

# Restore NuGet packages
dotnet restore

# Build in Release mode
dotnet build -c Release
```

## Architecture Overview

### Core Analysis Engine
The system follows an **Analyzer Pattern** with orchestrated execution:

- **AnalyzerOrchestrator**: Coordinates multiple analyzers with configurable execution strategies (sequential, parallel, prioritized)
- **AnalyzerBase**: Base class providing common functionality (validation, error handling, reporting)
- **AnalysisContext**: Shared state container for analyzers to communicate via SetSharedData/GetSharedData
- **Individual Analyzers**: Each focuses on specific aspects (plugins, memory, settings, paths, FCX mode)

### Multi-Layered Architecture

```
Scanner111.CLI/Desktop (UI Layer)
    ↓
Scanner111.Core (Business Logic)
    ├── Orchestration/ (Coordination)
    ├── Analysis/ (Core Analyzers)  
    ├── Services/ (Cross-cutting)
    ├── Configuration/ (YAML Settings)
    ├── Discovery/ (Path Detection)
    ├── Reporting/ (Output Generation)
    └── Data/ (FormID Lookups)
```

### Key Patterns

**Dependency Injection**: Heavy use of Microsoft.Extensions.DependencyInjection with proper service lifetimes
**Async-First**: All I/O operations use async/await with ConfigureAwait(false) for library code
**Thread-Safe Caching**: ConcurrentDictionary and SemaphoreSlim for cross-instance coordination
**Report Fragment Composition**: Hierarchical report building with nested fragments

### Critical Async Patterns

**Global Static Coordination**: FcxModeHandler uses static SemaphoreSlim for cross-instance caching
**Resource Management**: Implements both IDisposable and IAsyncDisposable consistently
**Cancellation Propagation**: All async methods accept CancellationToken and check cancellation

### YAML Configuration System
- **AsyncYamlSettingsCore**: Thread-safe YAML loading with caching
- **YamlStore Enum**: Defines different configuration file types (Main, Game, Settings, etc.)
- **Path Resolution**: Dynamic path building based on game type and store type

### Security Considerations
- **SQL Injection Protection**: FormIdDatabasePool validates table names against whitelist
- **Input Validation**: All user paths and configuration values are validated
- **Resource Limits**: Connection pooling and semaphore-based concurrency control

## Test Architecture

### Test Organization
- **Unit Tests**: Individual analyzer testing with mocked dependencies
- **Integration Tests**: Multi-analyzer coordination and real file system interaction  
- **Avalonia UI Tests**: Using Avalonia.Headless for ViewModel testing

### Key Testing Patterns
```csharp
// Async test with proper setup
[Fact] 
public async Task AnalyzeAsync_WithValidInput_ReturnsExpectedResult()
{
    // Arrange - Use NSubstitute for mocking
    var mockService = Substitute.For<IService>();
    mockService.GetDataAsync(Arg.Any<string>()).Returns(Task.FromResult(testData));

    // Act
    var result = await analyzer.AnalyzeAsync(context, cancellationToken);

    // Assert - Use FluentAssertions
    result.Success.Should().BeTrue();
    result.Fragment.Title.Should().Contain("Expected Title");
}
```

## Development Principles

### Thread Safety Requirements
- Use ConcurrentDictionary for shared state
- SemaphoreSlim for async coordination (never `lock` in async code)  
- Interlocked for atomic operations
- Document thread-safety in XML comments

### Async/Await Standards
- Always ConfigureAwait(false) in non-UI library code
- Never use .Result or .Wait() - always await
- Implement IAsyncDisposable for async cleanup
- Pass CancellationToken to all operations
- Handle OperationCanceledException appropriately

### Error Handling Strategy
- Domain-specific exception types
- Result<T> pattern for expected failures
- Structured logging with correlation IDs
- Actionable error messages with context

## Project-Specific Guidelines

### Porting from Legacy Code
1. Reference "Code to Port/" directory for original functionality (READ-ONLY)
2. Use sample_logs/ for test input validation (READ-ONLY)
3. Validate against sample_output/ for compatibility (READ-ONLY)
4. Replace "CLASSIC" references with "Scanner111"
5. Modernize with C# best practices while maintaining compatibility

### Core Business Logic Location
- **Scanner111.Core**: ALL non-UI logic must be placed here
- Separate interfaces from implementations  
- Organize by domain/feature areas
- Keep DTOs in dedicated Models/ folders

### Key Reminders
- Never modify READ-ONLY directories (Code to Port, sample_logs, sample_output)
- Always write tests first (TDD approach)
- Use proper disposal patterns for all resources
- Validate all inputs early with clear error messages
- Avoid replicating Python patterns - use C# idioms instead
- Query YAML files directly rather than caching frequently-used values