# Scanner111 AI Coding Agent Instructions

## Project Overview

Scanner111 is a modern C# port of the CLASSIC crash log analyzer for Bethesda games. It analyzes crash logs from Buffout 4 (Fallout 4) and Crash Logger (Skyrim), identifies problematic plugins, and provides detailed reports. The project follows an **Analyzer Pattern** with orchestrated execution for maximum extensibility and performance.

## Essential Development Commands

```bash
# Building and running
dotnet build                                    # Build entire solution
dotnet test --verbosity normal                  # Run all tests with output
dotnet test --filter "Category=Unit"           # Run specific test category
dotnet test --filter "AnalyzerOrchestratorTests" # Run specific test class
dotnet run --project Scanner111.CLI            # Run CLI application
dotnet run --project Scanner111.Desktop        # Run Avalonia desktop app

# Advanced test workflows
./run-all-tests.ps1                           # Comprehensive test suite with reporting
./run-all-tests.ps1 -Category Unit -Coverage  # Category-specific with coverage
./run-fast-tests.ps1                          # Skip slow performance tests
./run-coverage.ps1                            # Generate detailed coverage reports

# Development workflow
dotnet clean && dotnet restore                 # Clean slate dependency refresh
dotnet build -c Release                        # Production build
dotnet test --collect:"XPlat Code Coverage"    # Run tests with coverage
```

## Architecture: Analyzer Pattern with Orchestrated Execution

### Core Orchestration Flow
1. **AnalyzerOrchestrator** coordinates analyzer execution with configurable strategies (sequential, parallel, prioritized)
2. **AnalyzerBase** provides common functionality (validation, timeout handling, error management)
3. **AnalysisContext** serves as shared state container for inter-analyzer communication via `SetSharedData/GetSharedData`
4. **Individual Analyzers** focus on specific domains (FormID lookups, plugin analysis, settings validation, path checking, FCX mode)

### Multi-Layered Architecture

```
Scanner111.CLI/Desktop (UI Layer - Avalonia)
    ↓
Scanner111.Core (Business Logic - .NET 9)
    ├── Orchestration/          # Analyzer coordination and execution strategies
    ├── Analysis/Analyzers/     # Domain-specific analysis components
    ├── Services/               # Cross-cutting concerns (settings, I/O, caching)
    ├── Configuration/          # YAML settings management
    ├── Discovery/              # Game and file path detection
    ├── Reporting/              # Report generation and composition
    └── Data/                   # FormID database and lookups
```

### Dependency Injection Pattern
Extensive use of `Microsoft.Extensions.DependencyInjection` with proper service lifetimes:
```csharp
// In Program.cs or DI configuration
services.AddAnalyzerOrchestration(builder =>
{
    builder.AddAnalyzer<CustomAnalyzer>()
           .ConfigureDefaultOptions(opts => opts.Strategy = ExecutionStrategy.Parallel);
});

// In analyzer constructors - always validate dependencies
public MyAnalyzer(ILogger<MyAnalyzer> logger, ISettingsService settings)
    : base(logger)
{
    _settings = settings ?? throw new ArgumentNullException(nameof(settings));
}
```

## Critical Development Patterns

### Analyzer Implementation Template
```csharp
public sealed class MyAnalyzer : AnalyzerBase
{
    public MyAnalyzer(ILogger<MyAnalyzer> logger, /* dependencies */) : base(logger) { }
    
    public override string Name => "MyAnalyzer";
    public override string DisplayName => "My Custom Analyzer";
    public override int Priority => 50; // Lower = higher priority
    public override TimeSpan Timeout => TimeSpan.FromSeconds(30);
    
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        // Always check cancellation early
        cancellationToken.ThrowIfCancellationRequested();
        
        // Use shared data for inter-analyzer communication
        if (context.GetSharedData<bool>("SomeFlag"))
        {
            // React to previous analyzer results
        }
        
        // Always use ConfigureAwait(false) in library code
        var result = await SomeAsyncOperation().ConfigureAwait(false);
        
        // Set shared data for subsequent analyzers
        context.SetSharedData("ProcessedData", result);
        
        // Return structured results
        var fragment = ReportFragment.CreateSection("Analysis Results", result);
        return AnalysisResult.CreateSuccess(Name, fragment);
    }
}
```

### Async/Await Standards
- **Always use `ConfigureAwait(false)`** in library code (non-UI)
- **Never use `.Result` or `.Wait()`** - always await
- **Always accept and check `CancellationToken`**
- **Implement both `IDisposable` and `IAsyncDisposable`** for resource cleanup

### Thread Safety Requirements
```csharp
// Use ConcurrentDictionary for shared state
private readonly ConcurrentDictionary<string, object> _cache = new();

// SemaphoreSlim for async coordination (never 'lock' in async code)
private readonly SemaphoreSlim _semaphore = new(1, 1);

// Interlocked for atomic operations
private long _counter;
Interlocked.Increment(ref _counter);
```

### YAML Configuration Pattern
```csharp
// Thread-safe YAML access via AsyncYamlSettingsCore
var setting = await _yamlCore.GetSettingAsync<string>(
    YamlStore.Settings, 
    "section.key", 
    defaultValue, 
    cancellationToken).ConfigureAwait(false);

// Batch operations for multiple settings
var requests = new[]
{
    new SettingRequest<string>(YamlStore.Settings, "path1"),
    new SettingRequest<bool>(YamlStore.Game, "enabled")
};
var results = await _yamlCore.GetMultipleSettingsAsync(requests).ConfigureAwait(false);
```

## Project-Specific Guidelines

### Porting from Legacy Python Code
1. **Reference `Code to Port/` directory** for original functionality (READ-ONLY)
2. **Use `sample_logs/` for test input validation** (READ-ONLY)
3. **Validate against `sample_output/` for compatibility** (READ-ONLY)
4. **Replace "CLASSIC" terminology** with "Scanner111" in new code
5. **Modernize patterns** - use C# idioms, not Python translations

### Core Business Logic Location
- **ALL non-UI logic belongs in `Scanner111.Core`**
- **Separate interfaces from implementations** in distinct files
- **Organize by domain/feature areas** (Analysis/, Configuration/, Services/)
- **Keep DTOs in dedicated Models/ folders**

### Test Implementation Standards
```csharp
[Fact]
public async Task AnalyzeAsync_WithValidInput_ReturnsExpectedResult()
{
    // Arrange - Use NSubstitute for mocking
    var mockService = Substitute.For<IService>();
    mockService.GetDataAsync(Arg.Any<string>()).Returns(Task.FromResult(testData));
    
    var analyzer = new MyAnalyzer(_logger, mockService);
    var context = new AnalysisContext("test.log", _mockYamlCore);
    
    // Act
    var result = await analyzer.AnalyzeAsync(context);
    
    // Assert - Use FluentAssertions
    result.Success.Should().BeTrue();
    result.Fragment.Title.Should().Contain("Expected Title");
    result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
}
```

## Integration Points

### Report Fragment Composition
- **Hierarchical report building** with nested fragments
- **Ordered sections** via fragment `Order` property (lower = displayed first)
- **Fragment types**: Section, Warning, Error, Info, List
- **Cross-analyzer coordination** via `AnalysisContext.SetSharedData()`

### FormID Database System
- **Connection pooling** via `FormIdDatabasePool` with SQL injection protection
- **Async-first lookups** with sync fallbacks for compatibility
- **Table name validation** against whitelist for security
- **Batch operations** for performance optimization

### FCX Mode Coordination
- **Global static coordination** via `FcxModeHandler` with `SemaphoreSlim`
- **Cross-instance caching** to avoid duplicate expensive operations
- **Thread-safe result sharing** between analyzer instances

### Security Patterns
```csharp
// FormID Database - SQL injection prevention
private static readonly HashSet<string> ValidTableNames = new(StringComparer.OrdinalIgnoreCase)
{
    "Skyrim", "SkyrimSE", "Fallout4", "FO4", "Fallout76", "Morrowind", "Oblivion"
};

// Pre-built query templates - no string interpolation
var queryTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Skyrim"] = "SELECT entry FROM Skyrim WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE"
};
```

### Test Categories and Organization
```csharp
// Use standardized test traits for discovery
[Trait("Category", "Unit")]           // Fast, isolated tests
[Trait("Category", "Integration")]    // Multi-component tests
[Trait("Category", "Database")]       // FormID database tests
[Trait("Performance", "Fast")]        // < 100ms tests
[Trait("Performance", "Medium")]      // 100ms - 1s tests  
[Trait("Performance", "Slow")]        // > 1s tests
[Trait("Component", "Orchestration")] // System under test
```

## Common Anti-Patterns

1. ❌ **Sync over async** (`Task.Result`, `Task.Wait()`) → ✅ **Proper awaiting**
2. ❌ **Missing ConfigureAwait(false)** → ✅ **Always use in library code**
3. ❌ **Unvalidated dependencies** → ✅ **Null checks in constructors**
4. ❌ **Ignoring CancellationToken** → ✅ **Check cancellation early and often**
5. ❌ **Mutable shared state** → ✅ **ConcurrentDictionary, immutable objects**
6. ❌ **Missing timeout handling** → ✅ **Override `Timeout` property appropriately**

## Key Architecture Files

- `Scanner111.Core/Analysis/AnalyzerBase.cs` - Foundation for all analyzers
- `Scanner111.Core/Orchestration/AnalyzerOrchestrator.cs` - Execution coordination
- `Scanner111.Core/Analysis/AnalysisContext.cs` - Shared state container
- `Scanner111.Core/Configuration/AsyncYamlSettingsCore.cs` - Thread-safe YAML access
- `Scanner111.Core/DependencyInjection/OrchestrationServiceExtensions.cs` - DI setup patterns
- `Scanner111.Core/Data/FormIdDatabasePool.cs` - SQL injection-safe database access
- `Scanner111.Core/Analysis/FcxModeHandler.cs` - Cross-instance coordination example
- `Scanner111.Core/Reporting/ReportComposer.cs` - Final report assembly
- `Scanner111.Test/Orchestration/AnalyzerOrchestratorTests.cs` - Integration test examples
- `run-all-tests.ps1` - Comprehensive test suite with categorized execution

## Development Workflow

### Error Handling Strategy
- **Domain-specific exception types** for different failure modes
- **Result<T> pattern** for expected failures instead of exceptions
- **Structured logging** with correlation IDs via Serilog
- **Actionable error messages** with specific remediation steps

### Performance Considerations
- **Async-first architecture** for I/O-bound operations
- **Connection pooling** for database operations
- **Memory-efficient processing** using streams and IAsyncEnumerable
- **Cancellation support** for long-running operations
- **Resource cleanup** via proper disposal patterns

### Advanced Testing Workflows
The project includes sophisticated PowerShell test runners:
- `./run-all-tests.ps1` - Complete test suite with ASCII banners and timing
- `./run-fast-tests.ps1` - Skip `Performance=Slow` tests for rapid iteration
- `./run-coverage.ps1` - Generate detailed coverage reports with metrics
- Test categorization enables targeted execution: Unit, Integration, Database, Performance levels

Remember: This is a **modern C# port**, not a Python translation. Use C# best practices and leverage the rich ecosystem while maintaining compatibility with the original CLASSIC functionality for users.
