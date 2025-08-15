using Microsoft.Extensions.Logging;
using Scanner111.CLI.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.CLI.Commands;

/// <summary>
///     Command for running FCX (File Integrity Check) operations
/// </summary>
public class FcxCommand : ICommand<FcxOptions>
{
    private readonly IApplicationSettingsService _appSettingsService;
    private readonly IBackupService _backupService;
    private readonly IHashValidationService _hashService;
    private readonly ILogger<FcxCommand> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly ICliSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlSettings;

    public FcxCommand(
        ICliSettingsService settingsService,
        IHashValidationService hashService,
        IBackupService backupService,
        IApplicationSettingsService appSettingsService,
        IYamlSettingsProvider yamlSettings,
        ILogger<FcxCommand> logger,
        IMessageHandler messageHandler)
    {
        _settingsService = Guard.NotNull(settingsService, nameof(settingsService));
        _hashService = Guard.NotNull(hashService, nameof(hashService));
        _backupService = Guard.NotNull(backupService, nameof(backupService));
        _appSettingsService = Guard.NotNull(appSettingsService, nameof(appSettingsService));
        _yamlSettings = Guard.NotNull(yamlSettings, nameof(yamlSettings));
        _logger = Guard.NotNull(logger, nameof(logger));
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
    }

    public async Task<int> ExecuteAsync(FcxOptions options)
    {
        try
        {
            // Note: MessageHandler is already injected via constructor

            // Load settings
            var settings = await _settingsService.LoadSettingsAsync();

            // Apply command line overrides
            if (!string.IsNullOrEmpty(options.GamePath))
                settings.GamePath = options.GamePath;
            if (!string.IsNullOrEmpty(options.ModsFolder))
                settings.ModsFolder = options.ModsFolder;
            if (!string.IsNullOrEmpty(options.IniFolder))
                settings.IniFolder = options.IniFolder;

            // Handle backup restore operation
            if (!string.IsNullOrEmpty(options.RestorePath)) return await HandleRestoreAsync(options, settings);

            // Run FCX checks
            return await RunFcxChecksAsync(options, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during FCX operation");
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> RunFcxChecksAsync(FcxOptions options, ApplicationSettings settings)
    {
        // Detect game path if not specified
        var gamePath = settings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
        {
            gamePath = GamePathDetection.TryDetectGamePath("Fallout4");

            if (string.IsNullOrEmpty(gamePath))
            {
                _messageHandler.ShowError("Could not detect game installation path. Please specify with --game-path");
                return 1;
            }
        }

        Console.WriteLine($"\n========== Running FCX integrity checks for {options.Game} ==========");
        _messageHandler.ShowInfo($"Game path: {gamePath}");

        // Set static properties for FCX functionality
        CrashLog.GameRootPath = gamePath;
        CrashLog.PluginsDirectory = settings.ModsFolder ?? Path.Combine(gamePath, "Data");
        CrashLog.IniDirectory = settings.IniFolder;
        CrashLog.Game = Enum.TryParse<GameType>(options.Game, true, out var gameType) ? gameType : GameType.Unknown;

        // Create a dummy crash log for the analyzer
        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            GamePath = gamePath
        };

        // Create file integrity analyzer
        var analyzer = new FileIntegrityAnalyzer(
            _hashService,
            _appSettingsService,
            _yamlSettings,
            MessageHandler.IsInitialized ? new CliMessageHandler(!options.DisableColors) : null!);

        var cancellationToken = CancellationToken.None;

        // Create backup if requested
        if (options.Backup && !options.CheckOnly)
        {
            _messageHandler.ShowInfo("Creating backup before checks...");
            var backupProgress = new Progress<BackupProgress>(p =>
            {
                if (!options.DisableProgress) _messageHandler.ShowInfo($"Backing up: {p.CurrentFile} ({p.Progress}%)");
            });

            var backupResult = await _backupService.CreateFullBackupAsync(
                gamePath,
                backupProgress,
                cancellationToken);

            if (backupResult.Success)
            {
                _messageHandler.ShowSuccess($"Backup created: {backupResult.BackupPath}");
            }
            else
            {
                _messageHandler.ShowError($"Backup failed: {backupResult.ErrorMessage}");
                return 1;
            }
        }

        // Run the analysis
        _messageHandler.ShowInfo("Running file integrity checks...");
        var result = await analyzer.AnalyzeAsync(crashLog, cancellationToken);

        // Display results
        if (result is FcxScanResult fcxResult)
        {
            DisplayFcxResults(fcxResult, options);

            // Save to output file if specified
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                await SaveResultsToFileAsync(fcxResult, options.OutputFile);
                _messageHandler.ShowSuccess($"Results saved to: {options.OutputFile}");
            }

            // Return success if no critical issues found
            return fcxResult.GameStatus == GameIntegrityStatus.Good ? 0 : 1;
        }

        _messageHandler.ShowWarning("FCX checks returned no results");
        return 1;
    }

    private async Task<int> HandleRestoreAsync(FcxOptions options, ApplicationSettings settings)
    {
        Console.WriteLine("\n========== Restoring from backup ==========");

        if (string.IsNullOrEmpty(options.RestorePath))
        {
            _messageHandler.ShowError("Restore path not specified. Please provide a backup path with --restore");
            return 1;
        }

        var gamePath = settings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
        {
            gamePath = GamePathDetection.TryDetectGamePath("Fallout4");

            if (string.IsNullOrEmpty(gamePath))
            {
                _messageHandler.ShowError("Could not detect game installation path. Please specify with --game-path");
                return 1;
            }
        }

        var progress = new Progress<BackupProgress>(p =>
        {
            if (!options.DisableProgress) _messageHandler.ShowInfo($"Restoring: {p.CurrentFile} ({p.Progress}%)");
        });

        var success = await _backupService.RestoreBackupAsync(
            options.RestorePath,
            gamePath,
            null,
            progress,
            CancellationToken.None);

        if (success)
        {
            _messageHandler.ShowSuccess("Backup restored successfully");
            return 0;
        }

        _messageHandler.ShowError("Failed to restore backup");
        return 1;
    }

    private void DisplayFcxResults(FcxScanResult result, FcxOptions options)
    {
        // Overall status
        var statusColor = result.GameStatus switch
        {
            GameIntegrityStatus.Good => ConsoleColor.Green,
            GameIntegrityStatus.Warning => ConsoleColor.Yellow,
            GameIntegrityStatus.Critical => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        Console.ForegroundColor = statusColor;
        Console.WriteLine($"\nGame Integrity Status: {result.GameStatus}");
        Console.ResetColor();

        // File checks
        if (result.FileChecks?.Any() == true)
        {
            Console.WriteLine("\nFile Integrity Checks:");
            foreach (var check in result.FileChecks)
            {
                var checkColor = check.Status switch
                {
                    FileStatus.Valid => ConsoleColor.Green,
                    FileStatus.Modified => ConsoleColor.Yellow,
                    FileStatus.Missing => ConsoleColor.Red,
                    FileStatus.Unknown => ConsoleColor.Gray,
                    _ => ConsoleColor.White
                };

                Console.ForegroundColor = checkColor;
                Console.WriteLine($"  [{check.Status}] {check.FilePath}");
                if (!string.IsNullOrEmpty(check.Message)) Console.WriteLine($"         {check.Message}");
                Console.ResetColor();
            }
        }

        // Hash validations
        if (options.ValidateHashes && result.HashValidations?.Any() == true)
        {
            Console.WriteLine("\nHash Validations:");
            foreach (var validation in result.HashValidations)
            {
                var validColor = validation.IsValid ? ConsoleColor.Green : ConsoleColor.Red;
                Console.ForegroundColor = validColor;
                Console.WriteLine($"  [{(validation.IsValid ? "VALID" : "INVALID")}] {validation.FilePath}");
                if (options.Verbose)
                {
                    Console.WriteLine($"         Expected: {validation.ExpectedHash}");
                    Console.WriteLine($"         Actual:   {validation.ActualHash}");
                }

                Console.ResetColor();
            }
        }

        // Additional messages
        if (result.Messages?.Any() == true)
        {
            Console.WriteLine("\nMessages:");
            foreach (var message in result.Messages) _messageHandler.ShowInfo($"  {message}");
        }
    }

    private async Task SaveResultsToFileAsync(FcxScanResult result, string outputPath)
    {
        var content = result.GenerateReport();
        await File.WriteAllTextAsync(outputPath, content);
    }
}