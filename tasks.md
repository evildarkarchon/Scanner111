﻿**Tasks for Porting CLASSIC to C# with Avalonia**

This document outlines the tasks required to port the CLASSIC application from Python/Qt to C# using Avalonia for the UI framework. The goal is to maintain the functionality of the original application while leveraging C# and Avalonia's capabilities.
1. **Project Setup & Core Architecture:**
    *   ~~Create a new Avalonia MVVM project in C#.~~
    *   ~~Define the project structure (e.g., `ViewModels`, `Views`, `Models`, `Services`, `Converters`).~~
    ~~*   Implement a C# equivalent for the settings management currently handled by YAML files (e.g., `CLASSIC Settings.yaml`). Consider using a library like `YamlDotNet` for YAML parsing and a dedicated settings service.~~
    ~~*   Re-architect the `GlobalRegistry` concept. Consider using Dependency Injection (DI) for services or a static service locator pattern if appropriate for C#.~~

2. **UI Implementation (Views & ViewModels):**
    *  Design the main window View (`MainWindow.axaml`) based on the structure in CLASSIC_Interface.py, including tabs for main functions, backups and more. This new UI will be based on modern UI principles and Avalonia's capabilities.
    *  Implement the main window ViewModel (`MainWindowViewModel.cs`) to handle the logic and data for the main window.
    *  Use data binding extensively between Views and ViewModels to ensure a responsive UI.
    *  Implement commands in ViewModels for button actions, utilizing Avalonia's command binding features.
    *  Use `ObservableCollection` for dynamic lists if any are needed, allowing the UI to update automatically when items are added or removed.
    *  Handle asynchronous operations (scans, update checks, Papyrus monitoring) in ViewModels using `async/await` and `Task` to keep the UI responsive, updating properties that Views are bound to.
    
    **Main Tab:**
        *   Implement ViewModels and View components for the main action buttons ("Scan Crash Logs", "Scan Game Files") as well as the pastebin download feature.
        *   Implement the output text box for displaying logs and scan results.
        *   Implement Progress indicators for long-running tasks (e.g., scanning, updating).
    
    **Settings Tab:**
        *  Implement ViewModels and View components for folder selection sections (INI, Mods, Custom Scan Path) with "Browse" functionality.
        *  Implement ViewModels and View components for checkboxes (FCX Mode, Simplify Logs, Update Check, VR Mode, etc.).
    
    **Articles Tab:**
        * Implement ViewModels and View components for the articles section, including buttons linking to URLs.

    **Backups Tab:**
        * Implement ViewModels and View components for backup/restore/remove sections (XSE, Reshade, Vulkan, ENB).
        * Implement the "OPEN CLASSIC BACKUPS" button (Change name to "Open Vault Backups").
    
    **Bottom Buttons/Status Bar:**
        * Implement ViewModels and View components for utility buttons (Check Updates, Papyrus Monitoring).
        * The Papyrus monitoring button will need to be checkable and update its style based on state.
   
    **Dialogs:**
        * Create an "About" dialog View and ViewModel.
        * Implement a "Help" popup mechanism.
        * Use Avalonia's built-in dialogs or create custom ones for file/folder selection and message boxes/notifications (e.g., for update results, errors).
    *  Implement a custom dialog for displaying the "About" information, including version and credits, based on the CustomAboutDialog class in CLASSIC_Interface.py.
    *  Implement a custom dialog for displaying help information, similar to the "Help" popup in CLASSIC_Interface.py.
3. **Porting Core Logic (Services & Models):**
    *   Translate the core scanning logic from CLASSIC_ScanLogs.py into a C# service. This includes parsing crash logs and identifying issues.
        *   Implement the `ScanCrashLogs` service to handle crash log analysis.
        *   Implement the `ScanGameFiles` service to handle game file scanning.
        *   Implement the `ScanMods` service to handle mod file scanning.
        *   Implement the `ScanPapyrusLog` service to handle Papyrus log analysis.
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
    *   If the FormID database management from formid_db_manager.py is to be integrated, port the SQLite database interaction logic.

4. **Application Lifecycle & Utilities:**
    *   Set up logging to a file (similar to `CLASSIC Journal.log`).
    *   Manage application data folders (e.g., `CLASSIC Data/`, `CLASSIC Backup/`).
    *   Handle application exit.

5. **Build and Deployment:**
    *   Configure the C# project to build a standalone executable.
    *   Consider Avalonia's cross-platform capabilities if targeting multiple operating systems.

6. **Testing:**
    *   Write unit tests for ViewModels and services.
    *   Perform manual UI testing to ensure all features work as expected.

7. **Suggestions for Future Enhancements:**
    *   Consider implementing a plugin system for future extensibility.
    *   Explore Avalonia's theming capabilities for a more modern look and feel.
    *   Investigate the possibility of integrating with other modding tools or communities.
    *   Create unit tests to match the xUnit testing framework.
    *   Add more advanced parsing options for the Papyrus log.
    *   Add filtering capabilities for the Papyrus log entries.
    *   Add the ability to export Papyrus log analysis to a file.
    *   Enhance the UI with more detailed information for the Papyrus Log Analysis feature.