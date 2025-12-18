# Tasks: Add Application Logging Infrastructure

## 1. Core Infrastructure

- [x] 1.1 Add `Microsoft.Extensions.Logging` package to `Scanner111.Common.csproj`
- [x] 1.2 Add `Serilog.Extensions.Logging` and `Serilog.Sinks.File` to `Scanner111.csproj` (file sink)
- [x] 1.3 Configure logging in `App.axaml.cs` DI container
- [x] 1.4 Create log file location helper (user's AppData folder)

## 2. Orchestration Services (High Priority)

- [x] 2.1 Add `ILogger<ScanGameOrchestrator>` to `ScanGameOrchestrator`
  - Log scanner start/completion with duration
  - Log scanner errors with full exception details
  - Log progress milestones
- [x] 2.2 Add `ILogger<LogOrchestrator>` to `LogOrchestrator`
  - Log pipeline stages (parse, analyze, report)
  - Log configuration loading
  - Log validation warnings
- [x] 2.3 Add `ILogger<ScanExecutor>` to `ScanExecutor`
  - Log batch scan start/completion
  - Log individual log processing

## 3. Service Instrumentation (Medium Priority)

- [x] 3.1 Add logging to `BSArchService` (external process execution)
- [x] 3.2 Add logging to `XseChecker` (file system operations)
- [x] 3.3 Add logging to `GamePathDetector` (registry/filesystem detection)
- [x] 3.4 Add logging to `DocsPathDetector` (path detection)
- [x] 3.5 Add logging to `FormIdAnalyzer` (database operations)
- [x] 3.6 Add logging to `UserSettingsService` (settings persistence)
- [x] 3.7 Add logging to `ConfigurationCache` (YAML loading)
- [x] 3.8 Add logging to `PapyrusMonitorService` (file monitoring)
- [x] 3.9 Add logging to `PastebinService` (HTTP operations)

## 4. User-Facing Features

- [x] 4.1 Add "Export Debug Log" command in GUI
- [x] 4.2 Display log file location in Settings/About

## 5. Testing

- [x] 5.1 Add unit tests for logging configuration
- [x] 5.2 Verify logging doesn't impact performance (benchmark if needed)
- [x] 5.3 Test log file rotation/cleanup

## Dependencies

- Task 2.x depends on Task 1.x (infrastructure must be in place first)
- Task 3.x can be parallelized after Task 1.x completes
- Task 4.x depends on Task 1.x
- Task 5.x depends on Tasks 1-4
