using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.GameScanning;

/// <summary>
///     Validates XSE plugin compatibility and Address Library installation.
/// </summary>
public class XsePluginValidator : IXsePluginValidator
{
    private readonly Dictionary<string, AddressLibVersionInfo> _fallout4AddressLibInfo = new()
    {
        ["VR"] = new AddressLibVersionInfo
        {
            VersionConst = "VR",
            Filename = "version-1-2-72-0.csv",
            Description = "Virtual Reality (VR) version",
            Url = "https://www.nexusmods.com/fallout4/mods/64879?tab=files",
            ApplicableGames = new[] { GameType.Fallout4VR }
        },
        ["OG"] = new AddressLibVersionInfo
        {
            VersionConst = "OG",
            Filename = "version-1-10-163-0.bin",
            Description = "Non-VR (Regular) version",
            Url = "https://www.nexusmods.com/fallout4/mods/47327?tab=files",
            ApplicableGames = new[] { GameType.Fallout4 }
        },
        ["NG"] = new AddressLibVersionInfo
        {
            VersionConst = "NG",
            Filename = "version-1-10-984-0.bin",
            Description = "Non-VR (Next-Gen) version",
            Url = "https://www.nexusmods.com/fallout4/mods/47327?tab=files",
            ApplicableGames = new[] { GameType.Fallout4 }
        }
    };

    private readonly ILogger<XsePluginValidator> _logger;
    private readonly List<string> _messageList = new();
    private readonly IApplicationSettingsService _settingsService;

    private readonly Dictionary<string, AddressLibVersionInfo> _skyrimAddressLibInfo = new()
    {
        ["VR"] = new AddressLibVersionInfo
        {
            VersionConst = "VR",
            Filename = "version-1-4-15-0.csv",
            Description = "Virtual Reality (VR) version",
            Url = "https://www.nexusmods.com/skyrimspecialedition/mods/58271?tab=files",
            ApplicableGames = new[] { GameType.SkyrimVR }
        },
        ["SE"] = new AddressLibVersionInfo
        {
            VersionConst = "SE",
            Filename = "version-1-6-1170-0.bin",
            Description = "Special Edition version",
            Url = "https://www.nexusmods.com/skyrimspecialedition/mods/32444?tab=files",
            ApplicableGames = new[] { GameType.SkyrimSE }
        }
    };

    private readonly IYamlSettingsProvider _yamlProvider;

    public XsePluginValidator(
        IApplicationSettingsService settingsService,
        IYamlSettingsProvider yamlProvider,
        ILogger<XsePluginValidator> logger)
    {
        _settingsService = settingsService;
        _yamlProvider = yamlProvider;
        _logger = logger;
    }

    public async Task<string> ValidateAsync()
    {
        await Task.Run(() =>
        {
            var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
            var pluginsPath = settings.PluginsFolder;
            var gameType = settings.GameType;

            // Check if game type is supported
            if (!IsSupportedGame(gameType))
            {
                _messageList.Add($"ℹ️ Address Library validation is not available for {gameType}.\n-----\n");
                return;
            }

            // Get game version to determine correct Address Library
            var gameExePath = settings.GameExecutablePath;
            if (string.IsNullOrEmpty(gameExePath) || !File.Exists(gameExePath))
            {
                _messageList.AddRange(FormatGameVersionNotDetectedMessage(gameType));
                return;
            }

            // Check plugins path
            if (string.IsNullOrEmpty(pluginsPath) || !Directory.Exists(pluginsPath))
            {
                _messageList.AddRange(FormatPluginsPathNotFoundMessage());
                return;
            }

            // Determine game version from executable
            var gameVersion = GetGameVersion(gameExePath);
            if (string.IsNullOrEmpty(gameVersion))
            {
                _messageList.AddRange(FormatGameVersionNotDetectedMessage(gameType));
                return;
            }

            // Determine relevant versions based on game type
            var (correctVersions, wrongVersions) = DetermineRelevantVersions(gameType);

            // Check for Address Library files
            var correctVersionExists = correctVersions.Any(version =>
                File.Exists(Path.Combine(pluginsPath, version.Filename)));
            var wrongVersionExists = wrongVersions.Any(version =>
                File.Exists(Path.Combine(pluginsPath, version.Filename)));

            if (correctVersionExists)
                _messageList.AddRange(FormatCorrectAddressLibMessage());
            else if (wrongVersionExists)
                _messageList.AddRange(
                    FormatWrongAddressLibMessage(correctVersions.FirstOrDefault() ?? new AddressLibVersionInfo()));
            else
                _messageList.AddRange(
                    FormatAddressLibNotFoundMessage(correctVersions.FirstOrDefault() ?? new AddressLibVersionInfo()));

            // Additional checks for XSE plugins
            CheckXsePluginCompatibility(pluginsPath, gameType);
        });

        return string.Join("", _messageList);
    }

    private bool IsSupportedGame(GameType gameType)
    {
        return gameType == GameType.Fallout4 ||
               gameType == GameType.Fallout4VR ||
               gameType == GameType.SkyrimSE ||
               gameType == GameType.SkyrimVR ||
               gameType == GameType.Skyrim;
    }

    private void CheckXsePluginCompatibility(string pluginsPath, GameType gameType)
    {
        // Determine XSE name and DLL based on game type
        var (xseName, xseDll, xseUrl) = GetXseInfo(gameType);

        var gameExeDir =
            Path.GetDirectoryName(_settingsService.LoadSettingsAsync().GetAwaiter().GetResult().GameExecutablePath) ??
            "";
        var xsePath = Path.Combine(gameExeDir, xseDll);

        if (!File.Exists(xsePath))
            _messageList.AddRange(new[]
            {
                $"# ⚠️ WARNING : {xseName} NOT DETECTED #\n",
                $"  {xseName} is required for many mods and the Address Library.\n",
                $"  Expected location: {xsePath}\n",
                $"  Download from: {xseUrl}\n-----\n"
            });

        // Check for common problematic plugin combinations
        CheckProblematicPlugins(pluginsPath, gameType);
    }

    private (string name, string dll, string url) GetXseInfo(GameType gameType)
    {
        return gameType switch
        {
            GameType.Fallout4 => ("F4SE", "f4se_1_10_984.dll", "https://www.nexusmods.com/fallout4/mods/42147"),
            GameType.Fallout4VR => ("F4SEVR", "f4sevr_0_6_20.dll", "https://www.nexusmods.com/fallout4/mods/42147"),
            GameType.SkyrimSE => ("SKSE64", "skse64_1_6_1170.dll",
                "https://www.nexusmods.com/skyrimspecialedition/mods/30379"),
            GameType.SkyrimVR => ("SKSEVR", "sksevr_2_0_12.dll",
                "https://www.nexusmods.com/skyrimspecialedition/mods/30457"),
            GameType.Skyrim => ("SKSE", "skse_1_7_3.dll", "https://www.nexusmods.com/skyrim/mods/100216"),
            _ => ("Unknown", "unknown.dll", "#")
        };
    }

    private void CheckProblematicPlugins(string pluginsPath, GameType gameType)
    {
        try
        {
            var files = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileName(f).ToLowerInvariant())
                .ToHashSet();

            // Check for conflicting plugin combinations based on game type
            var conflicts = GetConflictsForGame(gameType);

            foreach (var (plugin1, plugin2, message) in conflicts)
                if (files.Contains(plugin1) && files.Contains(plugin2))
                    _messageList.AddRange(new[]
                    {
                        "# ⚠️ PLUGIN CONFLICT DETECTED #\n",
                        $"  {message}\n",
                        $"  Files: {plugin1} and {plugin2}\n",
                        "  Consider removing one or checking mod compatibility notes.\n-----\n"
                    });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for problematic plugins");
        }
    }

    private List<(string plugin1, string plugin2, string message)> GetConflictsForGame(GameType gameType)
    {
        var conflicts = new List<(string, string, string)>();

        // Common conflicts for Skyrim games
        if (gameType == GameType.SkyrimSE || gameType == GameType.SkyrimVR || gameType == GameType.Skyrim)
            conflicts.AddRange(new[]
            {
                ("po3_tweaks.dll", "po3_simpledualsheath.dll", "Po3's Tweaks and Simple Dual Sheath can conflict"),
                ("enginefixes.dll", "ssefixesng.dll",
                    "Engine Fixes and SSE Fixes NG provide overlapping functionality"),
                ("displaytweaks.dll", "ssedisplaytweaks.dll", "Multiple display tweak plugins detected")
            });

        // Common conflicts for Fallout 4 games
        if (gameType == GameType.Fallout4 || gameType == GameType.Fallout4VR)
            conflicts.AddRange(new[]
            {
                ("f4z_ro_root.dll", "ba2extract.dll", "Multiple archive extractor plugins detected"),
                ("weapondebris.dll", "weapondebriscrashfix.dll", "Both weapon debris plugins detected")
            });

        return conflicts;
    }

    private string GetGameVersion(string exePath)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            return versionInfo.FileVersion ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting game version from executable");
            return string.Empty;
        }
    }

    private (List<AddressLibVersionInfo> correct, List<AddressLibVersionInfo> wrong)
        DetermineRelevantVersions(GameType gameType)
    {
        var addressLibInfo = GetAddressLibInfoForGame(gameType);

        var correct = addressLibInfo.Values
            .Where(v => v.ApplicableGames.Contains(gameType))
            .ToList();

        var wrong = addressLibInfo.Values
            .Where(v => !v.ApplicableGames.Contains(gameType))
            .ToList();

        return (correct, wrong);
    }

    private Dictionary<string, AddressLibVersionInfo> GetAddressLibInfoForGame(GameType gameType)
    {
        return gameType switch
        {
            GameType.Fallout4 or GameType.Fallout4VR => _fallout4AddressLibInfo,
            GameType.SkyrimSE or GameType.SkyrimVR or GameType.Skyrim => _skyrimAddressLibInfo,
            _ => new Dictionary<string, AddressLibVersionInfo>()
        };
    }

    private List<string> FormatGameVersionNotDetectedMessage(GameType gameType)
    {
        var addressLibInfo = GetAddressLibInfoForGame(gameType);
        var urls = string.Join(" or ", addressLibInfo.Values.Select(v => v.Url).Distinct());

        return new List<string>
        {
            "❓ NOTICE : Unable to detect game version\n",
            "  If you have Address Library installed, please check the path in your settings.\n",
            "  If you don't have it installed, you can find it on the Nexus.\n",
            $"  Links: {urls}\n-----\n"
        };
    }

    private List<string> FormatPluginsPathNotFoundMessage()
    {
        return new List<string> { "❌ ERROR: Could not locate plugins folder path in settings\n-----\n" };
    }

    private List<string> FormatCorrectAddressLibMessage()
    {
        return new List<string> { "✔️ You have the correct version of the Address Library file!\n-----\n" };
    }

    private List<string> FormatWrongAddressLibMessage(AddressLibVersionInfo correctVersionInfo)
    {
        return new List<string>
        {
            "❌ CAUTION: You have installed the wrong version of the Address Library file!\n",
            $"  Remove the current Address Library file and install the {correctVersionInfo.Description}.\n",
            $"  Link: {correctVersionInfo.Url}\n-----\n"
        };
    }

    private List<string> FormatAddressLibNotFoundMessage(AddressLibVersionInfo correctVersionInfo)
    {
        return new List<string>
        {
            "❓ NOTICE: Address Library file not found\n",
            $"  Please install the {correctVersionInfo.Description} for proper functionality.\n",
            $"  Link: {correctVersionInfo.Url}\n-----\n"
        };
    }

    private class AddressLibVersionInfo
    {
        public string VersionConst { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public GameType[] ApplicableGames { get; set; } = Array.Empty<GameType>();
    }
}