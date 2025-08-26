# Scanner111 Project Documentation

## Project Overview
Scanner111 is a modern C# port of the legacy application found in the "Code to Port" directory. This project prioritizes thread-safety, resource management, and async-safety while following C# best practices and test-driven development principles.

## Key Technologies
- **Language**: C# (.NET 9.0+)
- **GUI Framework**: Avalonia MVVM with ReactiveUI
- **CLI Framework**: Spectre.Console with CommandLineParser
- **Testing**: xUnit with FluentAssertions
- **Original Reference**: Code to Port directory (READ-ONLY)

## Important Directories
- **Code to Port/**: [READ-ONLY] Original application to be ported
- **sample_logs/**: [READ-ONLY] Sample input data for testing
- **sample_output/**: [READ-ONLY] Expected output examples for validation

## Development Principles

### Test-Driven Development (TDD)
- Write tests BEFORE implementation code
- Every public method must have corresponding unit tests
- Use xUnit as the test framework
- Use FluentAssertions for readable test assertions
- Maintain high code coverage (target >80%)
- Create integration tests for component interactions

### C# Best Practices

#### Thread Safety Requirements
- Use `ConcurrentDictionary` and other concurrent collections for shared state
- Implement `SemaphoreSlim` for async synchronization (never use `lock` in async code)
- Use `Interlocked` class for atomic operations
- Document thread-safety guarantees in XML comments
- Consider immutability where practical

#### Async/Await Patterns
- Always use `ConfigureAwait(false)` in non-UI library code
- Never use `.Result` or `.Wait()` - always await async methods
- Use `ValueTask` for frequently called methods that often complete synchronously
- Implement `IAsyncDisposable` for async cleanup
- Pass `CancellationToken` to all async operations
- Handle `OperationCanceledException` appropriately

#### Resource Management
- Implement `IDisposable` and/or `IAsyncDisposable` for all resources
- Use `using` statements or declarations consistently
- Properly dispose HttpClient, streams, and database connections
- Avoid finalizers unless absolutely necessary
- Consider object pooling for frequently allocated objects
- Monitor for resource leaks in tests

## GUI Development Guidelines (Avalonia)

### MVVM with ReactiveUI
- ViewModels must inherit from `ReactiveObject`
- Use `ReactiveCommand` for all commands
- Implement `IActivatableViewModel` for lifecycle management
- Use `WhenAnyValue` for property observations
- Keep Views code-behind minimal (logic belongs in ViewModels)
- Use `ObservableAsPropertyHelper` for derived properties

### Avalonia Best Practices
- Use `.axaml` extension for XAML files
- Implement proper data binding (no direct UI manipulation)
- Provide design-time data for XAML preview
- Create reusable styles and resources
- Test ViewModels independently from Views
- Handle UI thread marshalling correctly

## CLI Development Guidelines

### Spectre.Console Usage
- Use `AnsiConsole` for all console output
- Implement progress indicators for long-running operations
- Display data in tables for clarity
- Use color coding for different message types
- Handle console cancellation (Ctrl+C) gracefully
- Provide interactive prompts where appropriate

### CommandLineParser Configuration
- Use verb commands for different operations
- Provide comprehensive help text with examples
- Validate arguments early with clear error messages
- Support both short (-v) and long (--verbose) options
- Return standard exit codes (0 for success, non-zero for errors)
- Implement --version and --help as standard options

## Testing Requirements

### Unit Test Structure
```csharp
public class [ClassUnderTest]Tests
{
    private readonly IFixture _fixture = new Fixture();
    
    [Fact]
    public async Task MethodName_StateUnderTest_ExpectedBehavior()
    {
        // Arrange
        var sut = CreateSystemUnderTest();
        var input = _fixture.Create<InputType>();
        
        // Act
        var result = await sut.MethodAsync(input);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult);
    }
}
```

### Test Categories
- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test component interactions
- **UI Tests**: Use Avalonia.Headless for testing
- **Performance Tests**: Use BenchmarkDotNet when needed

## Code Standards

### Naming Conventions
- Interfaces: `I` prefix (e.g., `IScannerService`)
- Async methods: `Async` suffix (e.g., `ProcessAsync()`)
- Private fields: underscore prefix (e.g., `_scannerService`)
- Constants: PascalCase (e.g., `MaxRetryCount`)
- Events: `On` prefix (e.g., `OnScanCompleted`)

### Project Organization
- **Scanner111.Core**: All non-UI logic and interfaces must be placed here
  - Business logic implementations
  - Service interfaces and implementations
  - Domain models and DTOs
  - Data access abstractions
  - Utility classes and helpers
- One type per file
- Organize by feature/domain
- Separate interfaces from implementations
- Group related functionality in namespaces
- Keep DTOs/Models in dedicated folders

### Documentation Requirements
- XML documentation for all public APIs
- Include code examples in documentation
- Document thread-safety guarantees
- Explain complex algorithms
- Note any assumptions or limitations

## Performance Guidelines

### Memory Optimization
- Use `ArrayPool<T>` for temporary arrays
- Leverage `Span<T>` and `Memory<T>` for buffer operations
- Minimize allocations in hot paths
- Use value types for small, frequently used data
- Profile memory usage with dotMemory or similar

### Performance Testing
- Use BenchmarkDotNet for performance benchmarks
- Profile before optimizing
- Set performance baselines
- Monitor for performance regressions
- Document performance-critical code

## Error Handling Strategy

### Exception Handling
- Create domain-specific exception types
- Use Result<T> pattern for expected failures
- Always log exceptions with context
- Never swallow exceptions
- Provide actionable error messages
- Include relevant data in exception properties

### Logging Configuration
- Use structured logging (Serilog recommended)
- Include correlation IDs for tracing
- Use appropriate log levels
- Avoid logging sensitive data
- Configure environment-specific sinks

## Porting Guidelines

### When Porting from "Code to Port"
1. Understand the original functionality first
2. Write tests based on sample_logs and sample_output
3. Implement modern C# equivalents
4. Replace "CLASSIC" references with "Scanner111"
5. Improve upon original design where possible
6. Maintain backward compatibility for file formats

### Validation Against Samples
- Use sample_logs as test input data
- Validate output against sample_output
- Ensure compatibility with original formats
- Document any intentional deviations

## Common Patterns to Use

### Dependency Injection
```csharp
services.AddSingleton<IScannerService, ScannerService>();
services.AddScoped<IDataProcessor, DataProcessor>();
```

### Async Enumerable Pattern
```csharp
public async IAsyncEnumerable<T> ProcessAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var item in source.WithCancellation(ct))
    {
        yield return await TransformAsync(item, ct);
    }
}
```

### Reactive Properties
```csharp
private readonly ObservableAsPropertyHelper<bool> _isProcessing;
public bool IsProcessing => _isProcessing.Value;
```

## Build Configuration

### Project Settings
- Target .NET 9.0 or later
- Enable nullable reference types
- Treat warnings as errors in Release
- Enable code analysis rules
- Use deterministic builds

### Required NuGet Packages
- Avalonia and Avalonia.ReactiveUI
- Spectre.Console
- CommandLineParser
- xUnit, xUnit.Runner.VisualStudio
- FluentAssertions
- Serilog and relevant sinks

## Notes for Development

### Priority Order
1. Thread-safety and resource management
2. Comprehensive test coverage
3. Clean async/await implementation
4. Performance optimization
5. User experience improvements

### Key Reminders
- Never modify READ-ONLY directories
- Always write tests first (TDD)
- Ensure proper disposal of resources
- Use async/await correctly
- Document public APIs
- Handle cancellation tokens
- Validate all inputs
- Log important operations
- Do not attempt to use the GlobalRegistry system, it is essentially an implementation of dependency injection which C# does better on its own.