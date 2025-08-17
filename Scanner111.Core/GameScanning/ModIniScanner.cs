using IniParser;
using IniParser.Model;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.GameScanning;

/// <summary>
///     Scans and analyzes mod INI configuration files for problematic settings.
/// </summary>
public class ModIniScanner : IModIniScanner
{
    // Constants for config settings
    private const string ConsoleCommandSetting = "sStartingConsoleCommand";
    private const string ConsoleCommandSection = "General";

    private const string ConsoleCommandNotice =
        "In rare cases, this setting can slow down the initial game startup time for some players.\n" +
        "You can test your initial startup time difference by removing this setting from the INI file.\n-----\n";

    private readonly Dictionary<string, string> _configFilePaths = new();
    private readonly Dictionary<string, IniData> _configFiles = new();
    private readonly List<string> _duplicateFiles = new();
    private readonly ILogger<ModIniScanner> _logger;
    private readonly List<string> _messageList = new();
    private readonly IApplicationSettingsService _settingsService;

    // List of files and their VSync settings to check
    private readonly List<(string file, string section, string setting)> _vsyncSettings = new()
    {
        ("dxvk.conf", "dxgi", "syncInterval"),
        ("enblocal.ini", "ENGINE", "ForceVSync"),
        ("longloadingtimesfix.ini", "Limiter", "EnableVSync"),
        ("reshade.ini", "APP", "ForceVsync"),
        ("fallout4_test.ini", "CreationKit", "VSyncRender"),
        ("skyrim_test.ini", "CreationKit", "VSyncRender")
    };

    public ModIniScanner(
        IApplicationSettingsService settingsService,
        ILogger<ModIniScanner> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<string> ScanAsync()
    {
        await Task.Run(() =>
        {
            var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
            var gameRootPath = settings.GamePath;

            if (string.IsNullOrEmpty(gameRootPath) || !Directory.Exists(gameRootPath))
            {
                _messageList.Add("# ⚠️ WARNING : Game path not configured or doesn't exist #\n-----\n");
                return;
            }

            // Load all INI files from game directory
            LoadConfigFiles(gameRootPath);

            if (_configFiles.Count == 0)
            {
                _messageList.Add("ℹ️ No mod INI files found in the game directory.\n-----\n");
                return;
            }

            // Check for console command settings that might slow down startup
            CheckStartingConsoleCommand();

            // Check for VSync settings in various files
            var vsyncList = CheckVsyncSettings();

            // Apply fixes to various INI files
            ApplyAllIniFixes();

            // Report VSync settings if found
            if (vsyncList.Count > 0)
            {
                _messageList.Add("* NOTICE : VSYNC IS CURRENTLY ENABLED IN THE FOLLOWING FILES *\n");
                _messageList.AddRange(vsyncList);
            }

            // Report duplicate files if found
            CheckDuplicateFiles();
        });

        return string.Join("", _messageList);
    }

    private void LoadConfigFiles(string gameRootPath)
    {
        try
        {
            var parser = new FileIniDataParser();
            var iniFiles = Directory.GetFiles(gameRootPath, "*.ini", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\Saves\\") && !f.Contains("\\backup\\", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in iniFiles)
                try
                {
                    var fileName = Path.GetFileName(file).ToLowerInvariant();
                    var iniData = parser.ReadFile(file);

                    if (_configFiles.ContainsKey(fileName))
                    {
                        _duplicateFiles.Add(file);
                    }
                    else
                    {
                        _configFiles[fileName] = iniData;
                        _configFilePaths[fileName] = file;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to parse INI file: {file}");
                }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading config files");
            _messageList.Add($"# ❌ ERROR : Failed to load config files: {ex.Message} #\n-----\n");
        }
    }

    private void CheckStartingConsoleCommand()
    {
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        var gameName = settings.GameType.ToString().ToLowerInvariant();

        foreach (var (fileName, iniData) in _configFiles)
            if (fileName.StartsWith(gameName) &&
                iniData.Sections.ContainsSection(ConsoleCommandSection) &&
                iniData[ConsoleCommandSection].ContainsKey(ConsoleCommandSetting))
                _messageList.AddRange(new[]
                {
                    $"[!] NOTICE: {_configFilePaths[fileName]} contains the *{ConsoleCommandSetting}* setting.\n",
                    ConsoleCommandNotice
                });
    }

    private List<string> CheckVsyncSettings()
    {
        var vsyncList = new List<string>();

        foreach (var (file, section, setting) in _vsyncSettings)
        {
            var fileName = file.ToLowerInvariant();
            if (_configFiles.TryGetValue(fileName, out var iniData))
                if (iniData.Sections.ContainsSection(section) &&
                    iniData[section].ContainsKey(setting))
                {
                    var value = iniData[section][setting];
                    if (IsTruthy(value)) vsyncList.Add($"{_configFilePaths[fileName]} | SETTING: {setting}\n");
                }
        }

        // Check highfpsphysicsfix.ini separately
        if (_configFiles.TryGetValue("highfpsphysicsfix.ini", out var highFpsIni))
            if (highFpsIni.Sections.ContainsSection("Main") &&
                highFpsIni["Main"].ContainsKey("EnableVSync") &&
                IsTruthy(highFpsIni["Main"]["EnableVSync"]))
                vsyncList.Add($"{_configFilePaths["highfpsphysicsfix.ini"]} | SETTING: EnableVSync\n");

        return vsyncList;
    }

    private void ApplyAllIniFixes()
    {
        // Fix ESPExplorer hotkey
        if (_configFiles.TryGetValue("espexplorer.ini", out var espExplorer))
            if (espExplorer.Sections.ContainsSection("General") &&
                espExplorer["General"].ContainsKey("HotKey") &&
                espExplorer["General"]["HotKey"].Contains("; F10"))
                ApplyIniFix("espexplorer.ini", "General", "HotKey", "0x79", "INI HOTKEY");

        // Fix EPO particle count
        if (_configFiles.TryGetValue("epo.ini", out var epo))
            if (epo.Sections.ContainsSection("Particles") &&
                epo["Particles"].ContainsKey("iMaxDesired"))
                if (int.TryParse(epo["Particles"]["iMaxDesired"], out var maxDesired) && maxDesired > 5000)
                    ApplyIniFix("epo.ini", "Particles", "iMaxDesired", "5000", "INI PARTICLE COUNT");

        // Fix F4EE settings if present
        if (_configFiles.TryGetValue("f4ee.ini", out var f4ee))
        {
            // Fix head parts unlock setting
            if (f4ee.Sections.ContainsSection("CharGen") &&
                f4ee["CharGen"].ContainsKey("bUnlockHeadParts") &&
                f4ee["CharGen"]["bUnlockHeadParts"] == "0")
                ApplyIniFix("f4ee.ini", "CharGen", "bUnlockHeadParts", "1", "INI HEAD PARTS UNLOCK");

            // Fix face tints unlock setting
            if (f4ee.Sections.ContainsSection("CharGen") &&
                f4ee["CharGen"].ContainsKey("bUnlockTints") &&
                f4ee["CharGen"]["bUnlockTints"] == "0")
                ApplyIniFix("f4ee.ini", "CharGen", "bUnlockTints", "1", "INI FACE TINTS UNLOCK");
        }

        // Fix highfpsphysicsfix.ini loading screen FPS if present
        if (_configFiles.TryGetValue("highfpsphysicsfix.ini", out var highFpsFix))
            if (highFpsFix.Sections.ContainsSection("Limiter") &&
                highFpsFix["Limiter"].ContainsKey("LoadingScreenFPS"))
                if (float.TryParse(highFpsFix["Limiter"]["LoadingScreenFPS"], out var fps) && fps < 600.0f)
                    ApplyIniFix("highfpsphysicsfix.ini", "Limiter", "LoadingScreenFPS", "600.0",
                        "INI LOADING SCREEN FPS");
    }

    private void ApplyIniFix(string fileName, string section, string setting, string value, string fixDescription)
    {
        try
        {
            if (_configFiles.TryGetValue(fileName, out var iniData))
            {
                iniData[section][setting] = value;

                // Save the file
                var parser = new FileIniDataParser();
                parser.WriteFile(_configFilePaths[fileName], iniData);

                _logger.LogInformation($"> > > PERFORMED {fixDescription} FIX FOR {_configFilePaths[fileName]}");
                _messageList.Add($"> Performed {fixDescription} Fix For : {_configFilePaths[fileName]}\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to apply INI fix for {fileName}");
        }
    }

    private void CheckDuplicateFiles()
    {
        if (_duplicateFiles.Count > 0)
        {
            _messageList.Add("* NOTICE : DUPLICATES FOUND OF THE FOLLOWING FILES *\n");
            foreach (var file in _duplicateFiles.OrderBy(f => Path.GetFileName(f))) _messageList.Add($"{file}\n");
        }
    }

    private bool IsTruthy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "on";
    }
}