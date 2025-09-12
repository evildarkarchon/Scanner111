# Code Style and Conventions for Scanner111

## C# Language Features
- **C# 11+** with latest language features
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`)
- **File-scoped namespaces** preferred
- **Target-typed new** expressions where appropriate

## Async/Await Patterns
- **Always use async/await** for I/O operations
- **ConfigureAwait(false)** in all library code (Scanner111.Core)
- **Never use .Result or .Wait()** - always await
- **Pass CancellationToken** to all async operations
- **Implement IAsyncDisposable** for async cleanup
- **Handle OperationCanceledException** appropriately

## Thread Safety
- **ConcurrentDictionary** for shared state
- **SemaphoreSlim** for async coordination (never `lock` in async code)
- **Interlocked** for atomic operations
- **Document thread-safety** in XML comments

## Naming Conventions
- **Interfaces**: Prefix with `I` (e.g., `IAnalyzer`)
- **Async methods**: Suffix with `Async` (e.g., `AnalyzeAsync`)
- **Private fields**: Underscore prefix (e.g., `_logger`)
- **Constants**: PascalCase for public, UPPER_CASE for private

## Project Organization
- **Interfaces** separate from implementations
- **Models** in dedicated Models/ folders
- **Services** for cross-cutting concerns
- **Analyzers** inherit from AnalyzerBase
- **All business logic** in Scanner111.Core

## Testing Conventions
- **xUnit** for test framework
- **NSubstitute** for mocking
- **FluentAssertions** for assertions
- **Arrange-Act-Assert** pattern
- **Async tests** with proper cancellation tokens
- **Test traits** for categorization (Category, Performance, Component)

## Error Handling
- **Domain-specific exceptions** where appropriate
- **Result<T> pattern** for expected failures
- **Structured logging** with correlation IDs
- **Actionable error messages** with context

## Documentation
- **XML comments** for public APIs
- **Thread-safety documentation** where relevant
- **Cancellation behavior** documented

## Build Configuration
- **Warnings as errors**: CS1998, xUnit1031, CS0618
- **AvaloniaVersion**: 11.3.4
- **Source Link**: Disabled

## YAML Configuration
- Query directly rather than caching
- Use AsyncYamlSettingsCore for thread-safe loading
- YamlStore enum for different config types