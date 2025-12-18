# Change: Add Application Logging Infrastructure

## Why

Scanner111 currently has **no application logging infrastructure**. The original CLASSIC Python implementation uses a centralized `Logger` module for consistent logging across components. Without logging:

1. **Silent failures**: Exception handlers silently swallow errors (e.g., `catch (IOException) { }`)
2. **No diagnostics**: Users can't provide debug information when reporting issues
3. **No audit trail**: No visibility into what operations were performed during a scan
4. **Difficult debugging**: Developers have no way to trace execution flow

This proposal adds `Microsoft.Extensions.Logging` as the logging abstraction, providing structured logging throughout the application.

## What Changes

- **Add logging abstraction**: Integrate `Microsoft.Extensions.Logging` into DI container
- **Add file sink**: Configure logging to write to a file in the user's app data folder
- **Instrument orchestration services**: Add logging to `LogOrchestrator`, `ScanGameOrchestrator`, `ScanExecutor`
- **Instrument critical services**: Add logging to services with silent exception handlers
- **Add debug log export**: Allow users to export logs for bug reports

### Services requiring logging instrumentation (high priority)

| Service | Location | Issues |
|---------|----------|--------|
| `ScanGameOrchestrator` | `Orchestration/` | Scanner errors silently captured without logging |
| `LogOrchestrator` | `Orchestration/` | No visibility into analysis pipeline stages |
| `BSArchService` | `ScanGame/` | Process execution errors silently converted to error codes |
| `XseChecker` | `ScanGame/` | IOException handlers with no logging |
| `GamePathDetector` | `GamePath/` | Registry/filesystem errors silently ignored |
| `DocsPathDetector` | `DocsPath/` | Silent exception handling |
| `FormIdAnalyzer` | `Analysis/` | SQLite exceptions silently caught |
| `UserSettingsService` | `Settings/` | JSON parse errors silently handled |
| `ConfigurationCache` | `Configuration/` | YAML load errors not logged |
| `PapyrusMonitorService` | `Papyrus/` | File monitoring errors silently handled |
| `PastebinService` | `Pastebin/` | HTTP errors need logging |

## Impact

- **Affected specs**: None (no existing specs)
- **Affected code**:
  - `App.axaml.cs` (DI registration)
  - All orchestration services
  - Services with exception handling
- **New dependencies**: `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.File` (or `Serilog.Extensions.Logging` + `Serilog.Sinks.File`)
- **Breaking changes**: None (additive only)
