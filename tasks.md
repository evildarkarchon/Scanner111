**Tasks for Porting CLASSIC to C# with Avalonia**

This document outlines the tasks required to port the CLASSIC application from Python/Qt to C# using Avalonia for the UI framework. The goal is to maintain the functionality of the original application while leveraging C# and Avalonia's capabilities.
1.  **Project Setup & Core Architecture:**
    *   ~~Create a new Avalonia MVVM project in C#.~~
    *   ~~Define the project structure (e.g., `ViewModels`, `Views`, `Models`, `Services`, `Converters`).~~
    *   ~~Implement a C# equivalent for the settings management currently handled by YAML files (e.g., `CLASSIC Settings.yaml`). Consider using a library like `YamlDotNet` for YAML parsing and a dedicated settings service.~~
    *   ~~Re-architect the `GlobalRegistry` concept. Consider using Dependency Injection (DI) for services or a static service locator pattern if appropriate for C#.~~

2.  **Porting Core Logic (Services & Models):**
    *   ~~Translate the core scanning logic from CLASSIC_ScanLogs.py into a C# service. This includes parsing crash logs and identifying issues.~~
    *   Port the game and mod scanning functionalities from CLASSIC_ScanGame.py.
        *   Implement logic for scanning unpacked mod files (similar to `scan_mods_unpacked`).
        *   Implement logic for scanning archived mod files (`.ba2`), including interaction with `BSArch.exe` using `System.Diagnostics.Process` (similar to `scan_mods_archived`).
    *   Port the specific check modules:
        *   CheckCrashgen.py for Buffout/Crashgen settings.
        *   CheckXsePlugins.py for XSE plugin and Address Library checks.
        *   ScanModInis.py for mod INI file analysis.
        *   WryeCheck.py for Wrye Bash report analysis.
    *   Implement the file management features (Backup, Restore, Remove) from CLASSIC_ScanGame.py (function `game_files_manage`) as a C# service.
    *   Port the update checking mechanism from Update.py to a C# service using `HttpClient` for fetching data from GitHub and Nexus.
    *   Implement the Papyrus log monitoring functionality, including parsing stats and handling errors, as seen in CLASSIC_Interface.py and Papyrus.py. This will likely involve asynchronous file watching and parsing.
    *   ~~IIf the FormID database management from formid_db_manager.py is to be integrated, port the SQLite database interaction logic.~~

3.  **UI Implementation (Views & ViewModels):**
    *   Design the main window View (`MainWindow.axaml`) based on the structure in CLASSIC_Interface.py, including tabs for main functions and backups. This new UI will be based on modern UI principles and Avalonia's capabilities.
    *   Implement the main window layout using Avalonia's XAML syntax, ensuring it is responsive and user-friendly.
    *   ~~Create a `MainWindowViewModel.cs` to handle the logic and data for the main window.~~
    *   **Main Tab:**
        *   Implement ViewModels and View components for folder selection sections (INI, Mods, Custom Scan Path) with "Browse" functionality.
        *   Implement ViewModels and View components for the main action buttons ("SCAN CRASH LOGS", "SCAN GAME FILES").
        *   Implement ViewModels and View components for checkboxes (FCX Mode, Simplify Logs, Update Check, VR Mode, etc.).
        *   Implement the "Update Source" ComboBox.
        *   Implement the "Articles" section with buttons linking to URLs.
        *   Implement the output text box for displaying logs and scan results.
    *   **Backups Tab:**
        *   Implement ViewModels and View components for backup/restore/remove sections (XSE, Reshade, Vulkan, ENB).
        *   Implement the "OPEN CLASSIC BACKUPS" button.
    *   **Bottom Buttons/Status Bar:**
        *   Implement ViewModels and View components for utility buttons (Open Settings, Check Updates, Papyrus Monitoring, Exit).
        *   The Papyrus monitoring button will need to be checkable and update its style based on state.
    *   **Dialogs:**
        *   Create an "About" dialog View and ViewModel.
        *   Implement a "Help" popup mechanism.
        *   Use Avalonia's built-in dialogs or create custom ones for file/folder selection and message boxes/notifications (e.g., for update results, errors).

4.  **MVVM Implementation:**
    *   Use data binding extensively between Views and ViewModels.
    *   Implement commands in ViewModels for button actions.
    *   Use `ObservableCollection` for dynamic lists if any are needed.
    *   Handle asynchronous operations (scans, update checks, Papyrus monitoring) in ViewModels using `async/await` and `Task` to keep the UI responsive, updating properties that Views are bound to.

5.  **Application Lifecycle & Utilities:**
    *   Implement application initialization logic (similar to `initialize` in CLASSIC_Main.py and the `__main__` block of `CLASSIC_Interface.py`).
    *   Set up logging to a file (similar to `CLASSIC Journal.log`).
    *   Manage application data folders (e.g., `CLASSIC Data/`, `CLASSIC Backup/`).
    *   Handle application exit.

6.  **Build and Deployment:**
    *   Configure the C# project to build a standalone executable.
    *   Consider Avalonia's cross-platform capabilities if targeting multiple operating systems.

7.  **Testing:**
    *   Write unit tests for ViewModels and services.
    *   Perform manual UI testing to ensure all features work as expected.

8. **Suggestions for Future Enhancements:**
    *   Consider implementing a plugin system for future extensibility.
    *   Explore Avalonia's theming capabilities for a more modern look and feel.
    *   Investigate the possibility of integrating with other modding tools or communities.
    *   Fix the unit tests to match the xUnit testing framework.
    *   Add more advanced parsing options for the Papyrus log.
    *   Add filtering capabilities for the Papyrus log entries.
    *   Add the ability to export Papyrus log analysis to a file.
    *   Enhance the UI with more detailed information for the Papyrus Log Analysis feature.