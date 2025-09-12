# Suggested Commands for Scanner111 Development

## Build and Run Commands
```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Clean build artifacts
dotnet clean

# Restore NuGet packages
dotnet restore

# Run CLI application
dotnet run --project Scanner111.CLI

# Run Desktop application
dotnet run --project Scanner111.Desktop
```

## Testing Commands
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run fast unit tests only (PowerShell)
./run-fast-tests.ps1

# Run all tests with detailed reporting (PowerShell)
./run-all-tests.ps1

# Run tests with coverage (PowerShell)
./run-coverage.ps1

# Run integration tests only (PowerShell)
./run-integration-tests.ps1
```

## Test Filtering
```bash
# Run a specific test class
dotnet test --filter "FullyQualifiedName~Scanner111.Test.Analysis.Analyzers.PluginAnalyzerTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~AnalyzeAsync_WithCallStackMatches_ReturnsPluginSuspects"

# Filter by category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Database"

# Filter by performance
dotnet test --filter "Performance=Fast"
dotnet test --filter "Performance!=Slow"

# Filter by component
dotnet test --filter "Component=Analyzer"
dotnet test --filter "Component=Orchestration"
```

## Windows System Commands
```bash
# List files (PowerShell/Git Bash)
ls

# Change directory
cd <directory>

# Create directory
mkdir <directory>

# Remove file/directory
rm <file>

# View file contents
cat <file>

# Search in files (use ripgrep instead of grep)
rg <pattern>

# Git operations
git status
git diff
git add .
git commit -m "message"
```

## Important Notes
- Always use forward slashes in paths even on Windows
- Avoid using `findstr` on Windows (use `rg` instead)
- Never run `dotnet run` or `dotnet test` with `--no-build` option
- Do not output to `nul` on Windows (creates undeleteable file)