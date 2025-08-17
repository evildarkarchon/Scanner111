using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;

namespace Scanner111.Core.Analyzers;

/// <summary>
///     Analyzer that validates game documents folder and configuration files.
///     Detects OneDrive interference and validates INI file integrity.
/// </summary>
public class DocumentsValidationAnalyzer : IAnalyzer
{
    private readonly ILogger<DocumentsValidationAnalyzer> _logger;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    ///     Initialize the documents validation analyzer
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    /// <param name="logger">Logger for debug output</param>
    /// <param name="settingsService">Application settings service for FCX mode check</param>
    public DocumentsValidationAnalyzer(IYamlSettingsProvider yamlSettings, ILogger<DocumentsValidationAnalyzer> logger,
        IApplicationSettingsService settingsService)
    {
        _yamlSettings = yamlSettings;
        _logger = logger;
        _settingsService = settingsService;
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "Documents Validation";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 2;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    ///     Analyze documents folder configuration and INI files
    /// </summary>
    /// <param name="crashLog">The crash log to be analyzed</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task that represents the analysis operation, containing the analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        // Only run if FCX mode is enabled
        var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
        if (!settings.FcxMode)
            return new DocumentsValidationResult
            {
                AnalyzerName = Name,
                Success = true,
                HasFindings = false,
                ReportLines = new List<string> { "Documents validation is only available in FCX mode.\n" }
            };

        var reportLines = new List<string>();

        try
        {
            _logger.LogDebug("Starting documents folder validation");

            // Check for cancellation at the start
            cancellationToken.ThrowIfCancellationRequested();

            // Get game information from crash log
            var gameType = crashLog.GameType ?? "Fallout4"; // Default to Fallout4 if not specified
            _logger.LogDebug($"Validating documents for game: {gameType}");

            var documentsPath = GetGameDocumentsPath(gameType);

            // Check folder configuration (OneDrive detection)
            var oneDriveDetected = await CheckFolderConfigurationAsync(reportLines, gameType, cancellationToken)
                .ConfigureAwait(false);

            // Check for cancellation before next step
            cancellationToken.ThrowIfCancellationRequested();

            // Validate INI files
            var iniResults =
                await ValidateIniFilesAsync(reportLines, gameType, cancellationToken).ConfigureAwait(false);

            var validationResults = new DocumentsValidationResult
            {
                AnalyzerName = Name,
                ReportLines = reportLines,
                HasFindings = reportLines.Count > 0,
                Success = true,
                OneDriveDetected = oneDriveDetected,
                IniResults = iniResults,
                DocumentsPath = documentsPath
            };

            _logger.LogDebug($"Documents validation completed with {reportLines.Count} findings");
            return validationResults;
        }
        catch (OperationCanceledException)
        {
            // Let cancellation exceptions bubble up
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during documents validation");
            reportLines.Add($"❌ ERROR: Failed to validate documents folder: {ex.Message}\n");

            return new DocumentsValidationResult
            {
                AnalyzerName = Name,
                ReportLines = reportLines,
                HasFindings = true,
                Success = false,
                Errors = [ex.Message]
            };
        }
    }

    /// <summary>
    ///     Check for OneDrive and other problematic folder configurations
    /// </summary>
    /// <param name="reportLines">List to add report lines to</param>
    /// <param name="gameType">Game type (Fallout4, Skyrim, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if OneDrive was detected</returns>
    private async Task<bool> CheckFolderConfigurationAsync(List<string> reportLines, string gameType,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Make it async-ready

        try
        {
            // Get documents folder path for the game
            var documentsPath = GetGameDocumentsPath(gameType);

            if (string.IsNullOrEmpty(documentsPath))
            {
                _logger.LogWarning("Could not determine documents path for game: {GameType}", gameType);
                return false;
            }

            _logger.LogDebug($"Checking documents path: {documentsPath}");

            var oneDriveDetected = false;

            // Check for OneDrive in the path
            if (documentsPath.Contains("onedrive", StringComparison.OrdinalIgnoreCase))
            {
                oneDriveDetected = true;
                _logger.LogWarning($"OneDrive detected in documents path: {documentsPath}");

                // Get warning message from YAML settings
                var warningMessage = GetOneDriveWarningMessage();
                if (!string.IsNullOrEmpty(warningMessage))
                {
                    reportLines.Add(warningMessage);
                }
                else
                {
                    // Fallback warning message
                    reportLines.Add("❌ WARNING: OneDrive detected in your documents folder path!\n");
                    reportLines.Add("   OneDrive can interfere with game files and cause various issues.\n");
                    reportLines.Add("   Consider moving your documents folder outside of OneDrive sync.\n");
                    reportLines.Add("-----\n");
                }
            }

            // Check if documents folder exists and is accessible
            if (!Directory.Exists(documentsPath))
            {
                reportLines.Add($"❌ WARNING: Game documents folder does not exist: {documentsPath}\n");
                reportLines.Add("   You may need to run the game at least once to create this folder.\n");
                reportLines.Add("-----\n");
            }

            return oneDriveDetected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking folder configuration");
            reportLines.Add($"❌ ERROR: Failed to check folder configuration: {ex.Message}\n");
            return false;
        }
    }

    /// <summary>
    ///     Validate game INI files
    /// </summary>
    /// <param name="reportLines">List to add report lines to</param>
    /// <param name="gameType">Game type (Fallout4, Skyrim, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of INI file validation results</returns>
    private async Task<Dictionary<string, IniValidationResult>> ValidateIniFilesAsync(List<string> reportLines,
        string gameType, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, IniValidationResult>();
        var documentsPath = GetGameDocumentsPath(gameType);

        if (string.IsNullOrEmpty(documentsPath) ||
            !Directory.Exists(documentsPath)) return results; // Already handled in CheckFolderConfigurationAsync

        var iniFiles = new[]
        {
            $"{gameType}.ini",
            $"{gameType}Custom.ini",
            $"{gameType}Prefs.ini"
        };

        foreach (var iniFileName in iniFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result =
                await ValidateIniFileAsync(reportLines, documentsPath, iniFileName, gameType, cancellationToken)
                    .ConfigureAwait(false);
            results[iniFileName] = result;
        }

        return results;
    }

    /// <summary>
    ///     Validate a specific INI file
    /// </summary>
    /// <param name="reportLines">List to add report lines to</param>
    /// <param name="documentsPath">Path to documents folder</param>
    /// <param name="iniFileName">Name of the INI file to validate</param>
    /// <param name="gameType">Game type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>INI validation result</returns>
    private async Task<IniValidationResult> ValidateIniFileAsync(List<string> reportLines, string documentsPath,
        string iniFileName, string gameType, CancellationToken cancellationToken)
    {
        var iniPath = Path.Combine(documentsPath, iniFileName);
        var issues = new List<string>();

        _logger.LogDebug($"Validating INI file: {iniPath}");

        try
        {
            if (!File.Exists(iniPath))
            {
                HandleMissingIniFile(reportLines, iniFileName, gameType);
                return new IniValidationResult
                {
                    Exists = false,
                    IsValid = false,
                    IsReadOnly = false,
                    HasArchiveInvalidation = false,
                    Issues = ["File does not exist"]
                };
            }

            // Check if file is read-only
            var fileInfo = new FileInfo(iniPath);
            var isReadOnly = fileInfo.IsReadOnly;

            if (isReadOnly)
            {
                reportLines.Add($"[!] CAUTION: YOUR {iniFileName} FILE IS SET TO READ ONLY.\n");
                reportLines.Add("    PLEASE REMOVE THE READ ONLY PROPERTY FROM THIS FILE,\n");
                reportLines.Add("    SO THE GAME CAN MAKE THE REQUIRED CHANGES TO IT.\n");
                reportLines.Add("-----\n");
                issues.Add("File is read-only");
            }

            // Read and validate INI file
            var iniContent = await File.ReadAllTextAsync(iniPath, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(iniContent))
            {
                reportLines.Add($"❌ WARNING: {iniFileName} file is empty or corrupted!\n");
                reportLines.Add("   Consider deleting the file and running the game to regenerate it.\n");
                reportLines.Add("-----\n");
                issues.Add("File is empty or corrupted");

                return new IniValidationResult
                {
                    Exists = true,
                    IsValid = false,
                    IsReadOnly = isReadOnly,
                    HasArchiveInvalidation = false,
                    Issues = issues
                };
            }

            // Simple INI parsing - check for basic corruption
            var iniData = ParseSimpleIni(iniContent);
            var isValid = iniData != null;
            var hasArchiveInvalidation = false;

            if (!isValid)
            {
                reportLines.Add($"❌ CAUTION: Your {iniFileName} file is very likely broken, please create a new one\n");
                reportLines.Add($"   Delete this file from your Documents/My Games/{gameType} folder, then\n");
                reportLines.Add($"   run the game to generate a new {iniFileName} file.\n");
                reportLines.Add("-----\n");
                issues.Add("File parsing failed - likely corrupted");
            }
            else
            {
                reportLines.Add($"✔️ No obvious corruption detected in {iniFileName}, file seems OK!\n");
                reportLines.Add("-----\n");

                // Special handling for Custom.ini files
                if (iniFileName.Contains("Custom", StringComparison.OrdinalIgnoreCase))
                    hasArchiveInvalidation =
                        await ValidateCustomIniSettingsAsync(reportLines, iniData, cancellationToken)
                            .ConfigureAwait(false);
            }

            return new IniValidationResult
            {
                Exists = true,
                IsValid = isValid,
                IsReadOnly = isReadOnly,
                HasArchiveInvalidation = hasArchiveInvalidation,
                Issues = issues
            };
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied to INI file: {IniPath}", iniPath);
            reportLines.Add($"[!] CAUTION: YOUR {iniFileName} FILE IS SET TO READ ONLY.\n");
            reportLines.Add("    PLEASE REMOVE THE READ ONLY PROPERTY FROM THIS FILE,\n");
            reportLines.Add("    SO THE GAME CAN MAKE THE REQUIRED CHANGES TO IT.\n");
            reportLines.Add("-----\n");

            return new IniValidationResult
            {
                Exists = true,
                IsValid = false,
                IsReadOnly = true,
                HasArchiveInvalidation = false,
                Issues = ["Access denied - file may be read-only"]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating INI file: {IniPath}", iniPath);
            reportLines.Add($"❌ ERROR: Failed to validate {iniFileName}: {ex.Message}\n");
            reportLines.Add("-----\n");

            return new IniValidationResult
            {
                Exists = File.Exists(iniPath),
                IsValid = false,
                IsReadOnly = false,
                HasArchiveInvalidation = false,
                Issues = [ex.Message]
            };
        }
    }

    /// <summary>
    ///     Handle missing INI file scenarios
    /// </summary>
    /// <param name="reportLines">List to add report lines to</param>
    /// <param name="iniFileName">Name of the missing INI file</param>
    /// <param name="gameType">Game type</param>
    private void HandleMissingIniFile(List<string> reportLines, string iniFileName, string gameType)
    {
        if (iniFileName.Equals($"{gameType}.ini", StringComparison.OrdinalIgnoreCase))
        {
            reportLines.Add($"❌ CAUTION: {iniFileName} FILE IS MISSING FROM YOUR DOCUMENTS FOLDER!\n");
            reportLines.Add($"   You need to run the game at least once with {gameType}Launcher.exe\n");
            reportLines.Add("   This will create files and INI settings required for the game to run.\n");
            reportLines.Add("-----\n");
        }
        else if (iniFileName.Contains("Custom", StringComparison.OrdinalIgnoreCase))
        {
            reportLines.Add("❌ WARNING: Archive Invalidation / Loose Files setting is not enabled.\n");
            reportLines.Add("   SCANNER111 recommends enabling this setting in the game INI files.\n");
            reportLines.Add("   This allows the game to load loose mod files properly.\n");
            reportLines.Add("-----\n");
        }
    }

    /// <summary>
    ///     Parse simple INI file content into a dictionary structure
    /// </summary>
    /// <param name="iniContent">Raw INI file content</param>
    /// <returns>Dictionary of sections and their key-value pairs, or null if parsing fails</returns>
    private Dictionary<string, Dictionary<string, string>>? ParseSimpleIni(string iniContent)
    {
        try
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var currentSection = "";
            var lines = iniContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
                    continue;

                // Check for section header
                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                {
                    currentSection = trimmedLine[1..^1].Trim();
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    continue;
                }

                // Check for key-value pair
                var equalIndex = trimmedLine.IndexOf('=');
                if (equalIndex > 0)
                {
                    var key = trimmedLine[..equalIndex].Trim();
                    var value = trimmedLine[(equalIndex + 1)..].Trim();

                    if (!string.IsNullOrEmpty(currentSection))
                    {
                        if (!result.ContainsKey(currentSection))
                            result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        result[currentSection][key] = value;
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing INI content");
            return null;
        }
    }

    /// <summary>
    ///     Validate Custom.ini specific settings (Archive Invalidation)
    /// </summary>
    /// <param name="reportLines">List to add report lines to</param>
    /// <param name="iniData">Parsed INI data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if Archive Invalidation is properly configured</returns>
    private async Task<bool> ValidateCustomIniSettingsAsync(List<string> reportLines,
        Dictionary<string, Dictionary<string, string>> iniData, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Make it async-ready

        try
        {
            // Check for Archive section and settings
            if (!iniData.TryGetValue("Archive", out var archiveSection) || archiveSection == null)
            {
                reportLines.Add("❌ WARNING: Archive Invalidation / Loose Files setting is not enabled.\n");
                reportLines.Add("   Consider adding [Archive] section with bInvalidateOlderFiles=1\n");
                reportLines.Add("   and sResourceDataDirsFinal= (empty) to enable loose files support.\n");
                reportLines.Add("-----\n");
                return false;
            }

            archiveSection.TryGetValue("bInvalidateOlderFiles", out var invalidateOlderFiles);
            archiveSection.TryGetValue("sResourceDataDirsFinal", out var resourceDataDirsFinal);

            var hasCorrectInvalidation = invalidateOlderFiles == "1";
            var hasCorrectResourceDirs = string.IsNullOrEmpty(resourceDataDirsFinal) || resourceDataDirsFinal == "";

            if (hasCorrectInvalidation && hasCorrectResourceDirs)
            {
                reportLines.Add("✔️ Archive Invalidation / Loose Files setting is already enabled!\n");
                reportLines.Add("-----\n");
                return true;
            }

            reportLines.Add("❌ WARNING: Archive Invalidation settings may not be configured correctly.\n");

            if (!hasCorrectInvalidation) reportLines.Add("   bInvalidateOlderFiles should be set to 1\n");

            if (!hasCorrectResourceDirs) reportLines.Add("   sResourceDataDirsFinal should be empty or blank\n");

            reportLines.Add("-----\n");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Custom.ini settings");
            reportLines.Add($"❌ ERROR: Failed to validate Custom.ini settings: {ex.Message}\n");
            reportLines.Add("-----\n");
            return false;
        }
    }

    /// <summary>
    ///     Get the documents folder path for the specified game
    /// </summary>
    /// <param name="gameType">Game type (Fallout4, Skyrim, etc.)</param>
    /// <returns>Documents folder path or empty string if not found</returns>
    private string GetGameDocumentsPath(string gameType)
    {
        try
        {
            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var gameFolder = Path.Combine(documentsFolder, "My Games", gameType);
            return gameFolder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents path for game: {GameType}", gameType);
            return string.Empty;
        }
    }

    /// <summary>
    ///     Get OneDrive warning message from YAML settings
    /// </summary>
    /// <returns>Warning message or empty string if not found</returns>
    private string GetOneDriveWarningMessage()
    {
        try
        {
            // Try to get warning message from YAML settings
            // This matches the Python implementation that loads from YAML.Main "Warnings_GAME.warn_docs_path"
            var mainYaml = _yamlSettings.LoadYaml<ClassicMainYaml>("CLASSIC Main");

            // For now, return a default message since we may not have the exact YAML structure
            // This can be enhanced once the YAML structure is better defined
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading OneDrive warning message from YAML");
            return string.Empty;
        }
    }
}

/// <summary>
///     Result from documents validation analysis
/// </summary>
public class DocumentsValidationResult : AnalysisResult
{
    /// <summary>
    ///     OneDrive detection result
    /// </summary>
    public bool OneDriveDetected { get; init; }

    /// <summary>
    ///     INI file validation results
    /// </summary>
    public Dictionary<string, IniValidationResult> IniResults { get; init; } = new();

    /// <summary>
    ///     Documents folder path that was validated
    /// </summary>
    public string? DocumentsPath { get; init; }
}

/// <summary>
///     Result of validating a single INI file
/// </summary>
public class IniValidationResult
{
    /// <summary>
    ///     Whether the INI file exists
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    ///     Whether the INI file is valid/parseable
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    ///     Whether the file is read-only
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    ///     Whether Archive Invalidation is properly configured (for Custom.ini)
    /// </summary>
    public bool HasArchiveInvalidation { get; init; }

    /// <summary>
    ///     Any issues found with the INI file
    /// </summary>
    public List<string> Issues { get; init; } = new();
}