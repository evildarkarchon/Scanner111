# Task Completion Checklist for Scanner111

## Before Marking a Task Complete

### 1. Code Quality Checks
- [ ] All code follows C# idioms (not Python patterns)
- [ ] Async/await patterns used correctly
- [ ] ConfigureAwait(false) in library code
- [ ] CancellationToken passed and checked
- [ ] No .Result or .Wait() calls
- [ ] Thread-safe where necessary

### 2. Testing Requirements
- [ ] Unit tests written for new code
- [ ] Tests use proper async patterns
- [ ] Tests have appropriate timeouts
- [ ] Tests use cancellation tokens
- [ ] Test traits applied (Category, Performance, Component)
- [ ] All tests passing

### 3. Run Verification Commands
```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# For major changes, run comprehensive tests
./run-all-tests.ps1
```

### 4. Resource Management
- [ ] IDisposable implemented where needed
- [ ] IAsyncDisposable for async resources
- [ ] No resource leaks
- [ ] Proper using statements/declarations

### 5. Error Handling
- [ ] Input validation with clear messages
- [ ] Appropriate exception handling
- [ ] No swallowing exceptions
- [ ] Logging where appropriate

### 6. Code Organization
- [ ] Business logic in Scanner111.Core
- [ ] Interfaces separated from implementations
- [ ] DTOs in Models/ folders
- [ ] Follows existing patterns

### 7. Documentation
- [ ] XML comments for public APIs
- [ ] Thread-safety documented
- [ ] Complex logic explained

## Special Reminders
- Never modify READ-ONLY directories (Code to Port/, sample_logs/, sample_output/)
- Replace "CLASSIC" references with "Scanner111"
- Validate against sample outputs for compatibility
- Query YAML directly, don't cache values

## Build Warnings as Errors
The following will fail the build:
- CS1998: Async method without await
- xUnit1031: Test method issues
- CS0618: Using obsolete members