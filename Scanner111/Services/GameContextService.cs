using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
/// Service responsible for managing and providing game context information
/// </summary>
public partial class GameContextService : IGameContextService
{
    // The default game - can be modified to be configurable if needed
    private const string DefaultGame = "Fallout4";
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");

    private string _currentGame = DefaultGame;

    /// <summary>
    /// Gets the current game name
    /// </summary>
    /// <returns>The name of the current game</returns>
    public string GetCurrentGame()
    {
        return _currentGame;
    }

    /// <summary>
    /// Sets the current game name
    /// </summary>
    /// <param name="gameName">The game name to set</param>
    public void SetCurrentGame(string gameName)
    {
        if (!string.IsNullOrEmpty(gameName)) _currentGame = gameName;
    }

    /// <summary>
    /// Gets the current game version in VR.
    /// </summary>
    /// <returns>The version of the game in VR format.</returns>
    public string GetGameVr()
    {
        try
        {
            // Check if VR Mode is enabled in settings
            var isVrMode = YamlSettingsCache.Instance.GetSetting<bool>(YamlStore.Game, "VR Mode");

            // Return appropriate VR identifier based on mode
            return isVrMode ? "VR" : "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting game VR: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Performs a check on XSE plugins to identify potential issues.
    /// </summary>
    /// <returns>A string containing a report of detected issues with XSE plugins.</returns>
    public string CheckXsePlugins()
    {
        try
        {
            var messageList = new List<string>();

            // Get plugins path from settings
            var pluginsPath = YamlSettingsCache.Instance.GetSetting<string>(YamlStore.GameLocal,
                $"Game{GetGameVr()}_Info.Game_Folder_Plugins");
            if (string.IsNullOrEmpty(pluginsPath) || !Directory.Exists(pluginsPath))
            {
                return "❌ ERROR: Could not locate plugins folder path in settings\n-----\n";
            }

            // Get game exe path
            var gameExePath =
                YamlSettingsCache.Instance.GetSetting<string>(YamlStore.GameLocal,
                    $"Game{GetGameVr()}_Info.Game_File_EXE");
            if (string.IsNullOrEmpty(gameExePath) || !File.Exists(gameExePath))
            {
                return "❓ NOTICE : Unable to locate Address Library\n" +
                       "  If you have Address Library installed, please check the path in your settings.\n" +
                       "  If you don't have it installed, you can find it on the Nexus.\n" +
                       "  Link: Regular: https://www.nexusmods.com/fallout4/mods/47327?tab=files or VR: https://www.nexusmods.com/fallout4/mods/64879?tab=files\n-----\n";
            }

            // Determine if in VR mode
            var isVrMode = YamlSettingsCache.Instance.GetSetting<bool>(YamlStore.Settings, "VR Mode");

            // Define address library versions to check
            var versions = new Dictionary<string, (string filename, string description, string url)>
            {
                {
                    "VR",
                    ("version-1-2-72-0.csv", "Virtual Reality (VR) version",
                        "https://www.nexusmods.com/fallout4/mods/64879?tab=files")
                },
                {
                    "OG",
                    ("version-1-10-163-0.bin", "Non-VR (Regular) version",
                        "https://www.nexusmods.com/fallout4/mods/47327?tab=files")
                },
                {
                    "NG",
                    ("version-1-10-984-0.bin", "Non-VR (New Game) version",
                        "https://www.nexusmods.com/fallout4/mods/47327?tab=files")
                }
            };

            // Determine correct versions based on VR mode
            (string filename, string description, string url)[] correctVersions;
            (string filename, string description, string url)[] wrongVersions;

            if (isVrMode)
            {
                correctVersions = [versions["VR"]];
                wrongVersions = [versions["OG"], versions["NG"]];
            }
            else
            {
                correctVersions = [versions["OG"], versions["NG"]];
                wrongVersions = [versions["VR"]];
            }

            // Check for correct address library files

            var correctVersionExists =
                correctVersions.Any(version => File.Exists(Path.Combine(pluginsPath, version.filename)));

            var wrongVersionExists =
                wrongVersions.Any(version => File.Exists(Path.Combine(pluginsPath, version.filename)));

            if (correctVersionExists)
            {
                messageList.Add("✔️ You have the correct version of the Address Library file!\n-----\n");
            }
            else if (wrongVersionExists)
            {
                var correctVersion = correctVersions[0];
                messageList.Add("❌ CAUTION: You have installed the wrong version of the Address Library file!\n");
                messageList.Add(
                    $"  Remove the current Address Library file and install the {correctVersion.description}.\n");
                messageList.Add($"  Link: {correctVersion.url}\n-----\n");
            }
            else
            {
                var correctVersion = correctVersions[0];
                messageList.Add("❓ NOTICE: Address Library file not found\n");
                messageList.Add($"  Please install the {correctVersion.description} for proper functionality.\n");
                messageList.Add($"  Link: {correctVersion.url}\n-----\n");
            }

            return string.Join("", messageList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking XSE plugins: {ex.Message}");
            return $"Error checking XSE plugins: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks crash generation settings
    /// </summary>
    /// <returns>A string containing a report of any issues found with crash generation settings</returns>
    public string CheckCrashgenSettings()
    {
        try
        {
            var messageList = new List<string>();

            // Get plugins path from settings
            var pluginsPath = YamlSettingsCache.Instance.GetSetting<string>(YamlStore.GameLocal,
                $"Game{GetGameVr()}_Info.Game_Folder_Plugins");
            if (string.IsNullOrEmpty(pluginsPath) || !Directory.Exists(pluginsPath))
            {
                return "❌ ERROR: Could not locate plugins folder path in settings\n-----\n";
            }

            // Get crash generator name from settings
            var crashgenName =
                YamlSettingsCache.Instance.GetSetting<string>(YamlStore.Game,
                    $"Game{GetGameVr()}_Info.CRASHGEN_LogName") ?? "Buffout4";

            // Find config file
            string? configPath = null;
            var hasDuplicateConfig = false;
            var configPathOg = Path.Combine(pluginsPath, "Buffout4", "config.toml");
            var configPathVr = Path.Combine(pluginsPath, "Buffout4.toml");

            if (File.Exists(configPathOg) && File.Exists(configPathVr))
            {
                hasDuplicateConfig = true;
                configPath = configPathOg; // Use OG config if both exist
            }
            else if (File.Exists(configPathOg))
            {
                configPath = configPathOg;
            }
            else if (File.Exists(configPathVr))
            {
                configPath = configPathVr;
            }

            // Check for missing config files
            if (configPath == null)
            {
                messageList.Add(
                    $"# [!] NOTICE : Unable to find the {crashgenName} config file, settings check will be skipped. #\n");
                messageList.Add(
                    $"  To ensure this check doesn't get skipped, {crashgenName} has to be installed manually.\n");
                messageList.Add(
                    "  [ If you are using Mod Organizer 2, you need to run CLASSIC through a shortcut in MO2. ]\n-----\n");
                return string.Join("", messageList);
            }

            // Handle duplicate config files warning
            if (hasDuplicateConfig)
            {
                messageList.Add(
                    $"# ❌ CAUTION : BOTH VERSIONS OF {crashgenName.ToUpper()} TOML SETTINGS FILES WERE FOUND! #\n");
                messageList.Add(
                    $"When editing {crashgenName} toml settings, make sure you are editing the correct file.\n");
                messageList.Add(
                    $"Please recheck your {crashgenName} installation and delete any obsolete files.\n-----\n");
            }

            // Detect installed plugins
            var installedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var file in Directory.GetFiles(pluginsPath))
                {
                    installedPlugins.Add(Path.GetFileName(file).ToLower());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing plugins directory: {ex.Message}");
            }

            // Check settings based on installed plugins
            if (GetCurrentGame() == "Fallout4")
            {
                var hasXCell = installedPlugins.Contains("x-cell-fo4.dll") ||
                               installedPlugins.Contains("x-cell-og.dll") ||
                               installedPlugins.Contains("x-cell-ng2.dll");

                var hasAchievements = installedPlugins.Contains("achievements.dll") ||
                                      installedPlugins.Contains("achievementsmodsenablerloader.dll");

                var hasLooksMenu = installedPlugins.Any(plugin => plugin.Contains("f4ee"));

                var hasBakaScrapHeap = installedPlugins.Contains("bakascrapheap.dll");

                // Process settings
                ProcessTomlSetting(configPath, messageList, "Patches", "Achievements", hasAchievements,
                    false, "The Achievements Mod and/or Unlimited Survival Mode is installed",
                    $"to prevent conflicts with {crashgenName}");

                ProcessTomlSetting(configPath, messageList, "Patches", "MemoryManager", hasXCell,
                    false, "The X-Cell Mod is installed",
                    "to prevent conflicts with X-Cell", hasBakaScrapHeap);

                // Special case for BakaScrapHeap
                if (hasBakaScrapHeap && GetTomlValue(configPath, "Patches", "MemoryManager"))
                {
                    messageList.Add(
                        $"# ❌ CAUTION : The Baka ScrapHeap Mod is installed, but is redundant with {crashgenName} #\n");
                    messageList.Add(
                        $" FIX: Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {crashgenName}.\n-----\n");
                }

                ProcessTomlSetting(configPath, messageList, "Patches", "HavokMemorySystem", hasXCell,
                    false, "The X-Cell Mod is installed",
                    "to prevent conflicts with X-Cell");

                ProcessTomlSetting(configPath, messageList, "Patches", "BSTextureStreamerLocalHeap", hasXCell,
                    false, "The X-Cell Mod is installed",
                    "to prevent conflicts with X-Cell");

                ProcessTomlSetting(configPath, messageList, "Patches", "ScaleformAllocator", hasXCell,
                    false, "The X-Cell Mod is installed",
                    "to prevent conflicts with X-Cell");

                ProcessTomlSetting(configPath, messageList, "Patches", "SmallBlockAllocator", hasXCell,
                    false, "The X-Cell Mod is installed",
                    "to prevent conflicts with X-Cell");

                ProcessTomlSetting(configPath, messageList, "Patches", "ArchiveLimit",
                    configPath.ToLower().Contains("buffout4/config.toml"),
                    false, "Archive Limit is enabled",
                    "to prevent crashes");

                ProcessTomlSetting(configPath, messageList, "Compatibility", "F4EE", hasLooksMenu,
                    true, "Looks Menu is installed, but F4EE parameter is set to FALSE",
                    "to prevent bugs and crashes from Looks Menu");
            }

            return string.Join("", messageList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking crash generation settings: {ex.Message}");
            return $"Error checking crash generation settings: {ex.Message}";
        }
    }

    /// <summary>
    /// Scans for Wrye Bash issues
    /// </summary>
    /// <returns>A report of any issues found with Wrye Bash</returns>
    public string ScanWryeCheck()
    {
        try
        {
            // Constants for links
            const string troubleshootingLink = "https://www.nexusmods.com/fallout4/articles/4141";
            const string documentationLink = "https://wrye-bash.github.io/docs/";
            const string simpleEslifyLink = "https://www.nexusmods.com/skyrimspecialedition/mods/27568";

            // Load settings from YAML
            var missingHtmlMessage =
                YamlSettingsCache.Instance.GetSetting<string>(YamlStore.Game, "Warnings_MODS.Warn_WRYE_MissingHTML") ??
                string.Empty;
            var pluginCheckPath =
                YamlSettingsCache.Instance.GetSetting<string>(YamlStore.Game,
                    $"Game{GetGameVr()}_Info.Docs_File_WryeBashPC") ?? string.Empty;
            var wryeWarnings =
                YamlSettingsCache.Instance.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Warnings_WRYE");

            // Build the message list
            var messageList = new List<string>();

            // Check if report exists
            if (string.IsNullOrEmpty(pluginCheckPath) || !File.Exists(pluginCheckPath))
            {
                if (!string.IsNullOrEmpty(missingHtmlMessage))
                {
                    return missingHtmlMessage;
                }

                return "ERROR: Wrye Bash plugin checker report not found!";
            }

            // Start building the report
            messageList.Add("\n✔️ WRYE BASH PLUGIN CHECKER REPORT WAS FOUND! ANALYZING CONTENTS...\n");
            messageList.Add($"  [This report is located in your Documents/My Games/{GetCurrentGame()} folder.]\n");
            messageList.Add("  [To hide this report, remove *ModChecker.html* from the same folder.]\n");

            // Parse the HTML report
            var reportContents = ParseWryeReport(pluginCheckPath, wryeWarnings!);
            messageList.AddRange(reportContents);

            // Add resource links
            messageList.Add("\n❔ For more info about the above detected problems, see the WB Advanced Readme\n");
            messageList.Add("  For more details about solutions, read the Advanced Troubleshooting Article\n");
            messageList.Add($"  Advanced Troubleshooting: {troubleshootingLink}\n");
            messageList.Add($"  Wrye Bash Advanced Readme Documentation: {documentationLink}\n");
            messageList.Add("  [ After resolving any problems, run Plugin Checker in Wrye Bash again! ]\n\n");

            return string.Join("", messageList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning Wrye Bash report: {ex.Message}");
            return $"Error scanning Wrye Bash report: {ex.Message}";
        }
    }

    /// <summary>
    /// Scans mod INI files for issues.
    /// </summary>
    /// <returns>A report detailing any identified issues with mod INI files.</returns>
    public string ScanModInis()
    {
        try
        {
            var messageList = new List<string>();

            // Get game folder path
            var gameFolderPath =
                YamlSettingsCache.Instance.GetSetting<string>(YamlStore.GameLocal,
                    $"Game{GetGameVr()}_Info.Root_Folder_Game");
            if (string.IsNullOrEmpty(gameFolderPath) || !Directory.Exists(gameFolderPath))
            {
                return "❌ ERROR: Could not locate game folder path in settings\n-----\n";
            }

            // Initialize config file dictionary
            var configFiles = new Dictionary<string, string>();
            var duplicateFiles = new Dictionary<string, List<string>>();
            var duplicateWhitelist = new List<string> { "F4EE" };

            // Scan for config files
            ScanForConfigFiles(gameFolderPath, configFiles, duplicateFiles, duplicateWhitelist);

            // Check for console command settings
            CheckConsoleCommandSettings(configFiles, messageList);

            // Check for VSync settings
            var vsyncList = CheckVSyncSettings(configFiles);

            // Apply fixes to various INI files
            ApplyIniFixes(configFiles, messageList);

            // Report VSync settings if found
            if (vsyncList.Count > 0)
            {
                messageList.Add("* NOTICE : VSYNC IS CURRENTLY ENABLED IN THE FOLLOWING FILES *\n");
                messageList.AddRange(vsyncList);
            }

            // Report duplicate files if found
            if (duplicateFiles.Count <= 0) return string.Join("", messageList);
            var allDuplicates = new List<string>();

            // Collect paths from duplicate_files dictionary
            foreach (var paths in duplicateFiles.Values)
            {
                allDuplicates.AddRange(paths);
            }

            // Also add original files that have duplicates
            allDuplicates.AddRange(from file in configFiles.Keys
                where duplicateFiles.ContainsKey(file)
                select configFiles[file]);

            // Sort by filename for consistent output
            allDuplicates.Sort();

            messageList.Add("* NOTICE : DUPLICATES FOUND OF THE FOLLOWING FILES *\n");
            messageList.AddRange(allDuplicates.Select(path => $"{path}\n"));

            return string.Join("", messageList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning mod INIs: {ex.Message}");
            return $"Error scanning mod INIs: {ex.Message}";
        }
    }

    /// <summary>
    /// Retrieves a boolean value from a specific section and key in a TOML configuration file.
    /// </summary>
    /// <param name="filePath">The path to the TOML file to read.</param>
    /// <param name="section">The section in the TOML file where the key is located.</param>
    /// <param name="key">The key whose value needs to be retrieved.</param>
    /// <returns>A boolean value corresponding to the key in the specified section, or false if the key is not found or an error occurs.</returns>
    private bool GetTomlValue(string filePath, string section, string key)
    {
        try
        {
            // Simple implementation - in a real app, use a proper TOML parser
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');

            var inSection = false;
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Check for section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    inSection = sectionName.Equals(section, StringComparison.OrdinalIgnoreCase);
                }
                // Check for key in current section
                else if (inSection && trimmedLine.StartsWith(key + " = "))
                {
                    var value = trimmedLine.Substring((key + " = ").Length).Trim();
                    return value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading TOML value: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets a value in a TOML configuration file under a specific section and key.
    /// </summary>
    /// <param name="filePath">The file path of the TOML configuration file.</param>
    /// <param name="section">The section in the TOML file to modify.</param>
    /// <param name="key">The key within the specified section to set the value for.</param>
    /// <param name="value">The boolean value to set for the specified key.</param>
    private void SetTomlValue(string filePath, string section, string key, bool value)
    {
        try
        {
            // Simple implementation - in a real app, use a proper TOML parser
            var content = File.ReadAllText(filePath);
            var lines = new List<string>(content.Split('\n'));

            var inSection = false;
            var keyFound = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmedLine = lines[i].Trim();

                // Check for section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    inSection = sectionName.Equals(section, StringComparison.OrdinalIgnoreCase);
                }
                // Check for key in current section
                else if (inSection && trimmedLine.StartsWith(key + " = "))
                {
                    lines[i] = $"{key} = {value.ToString().ToLower()}";
                    keyFound = true;
                    break;
                }
            }

            // Write updated content back to file
            if (keyFound)
            {
                File.WriteAllText(filePath, string.Join(Environment.NewLine, lines));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting TOML value: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a TOML setting, validating its current value and updating it if necessary.
    /// </summary>
    /// <param name="filePath">The path to the TOML file to be processed.</param>
    /// <param name="messages">A list to store messages detailing the outcome of the processing.</param>
    /// <param name="section">The section in the TOML file where the target setting resides.</param>
    /// <param name="key">The key of the setting to be validated or updated.</param>
    /// <param name="condition">A condition indicating whether the setting should be processed.</param>
    /// <param name="desiredValue">The desired value for the specified key in the TOML file.</param>
    /// <param name="description">A description of the setting being processed.</param>
    /// <param name="reason">The reason for changing the setting if required.</param>
    /// <param name="specialCase">An optional parameter indicating if this setting requires special handling, defaulting to false.</param>
    private void ProcessTomlSetting(string filePath, List<string> messages,
        string section, string key, bool condition, bool desiredValue,
        string description, string reason, bool specialCase = false)
    {
        if (!condition)
        {
            return;
        }

        var currentValue = GetTomlValue(filePath, section, key);
        if (currentValue != desiredValue)
        {
            messages.Add($"# ❌ CAUTION : {description}, but {key} parameter is set to {currentValue} #\n");
            messages.Add($"    Auto Scanner will change this parameter to {desiredValue} {reason}.\n-----\n");

            // Apply the change
            SetTomlValue(filePath, section, key, desiredValue);
            Console.WriteLine($"Changed {key} from {currentValue} to {desiredValue}");
        }
        else
        {
            // Setting is already correctly configured
            messages.Add($"✔️ {key} parameter is correctly configured in your Buffout4 settings!\n-----\n");
        }
    }

    /// <summary>
    /// Parses the Wrye Bash HTML report and extracts relevant sections and warnings.
    /// </summary>
    /// <param name="reportPath">The file path to the HTML report.</param>
    /// <param name="wryeWarnings">A dictionary of warnings associated with specific report sections.</param>
    /// <returns>A list of formatted message strings summarizing the report contents.</returns>
    private List<string> ParseWryeReport(string reportPath, Dictionary<string, string> wryeWarnings)
    {
        var messageList = new List<string>();

        // Read HTML file
        var htmlContent = File.ReadAllText(reportPath);

        // Very simple HTML parsing - in a real app, use a proper HTML parser like HtmlAgilityPack
        var sections = ExtractSections(htmlContent);

        foreach (var (title, plugins) in sections)
        {
            // Skip active plugins section
            if (title == "Active Plugins:")
                continue;

            // Format section header
            if (!string.IsNullOrEmpty(title))
            {
                messageList.Add(FormatSectionHeader(title));
            }

            // Handle special ESL Capable section
            if (title == "ESL Capable")
            {
                messageList.Add(
                    $"❓ There are {plugins.Count} plugins that can be given the ESL flag. This can be done with\n");
                messageList.Add("  the SimpleESLify script to avoid reaching the plugin limit (254 esm/esp).\n");
                messageList.Add("  SimpleESLify: https://www.nexusmods.com/skyrimspecialedition/mods/27568\n  -----\n");
            }

            // Add any matching warnings from settings
            if (wryeWarnings != null)
            {
                foreach (var warning in wryeWarnings)
                {
                    if (title?.Contains(warning.Key) == true)
                    {
                        messageList.Add(warning.Value);
                    }
                }
            }

            // List plugins (except for special sections)
            if (title is "ESL Capable" or "Active Plugins:") continue;
            messageList.AddRange(plugins.Select(plugin => $"    > {plugin}\n"));
        }

        return messageList;
    }

    /// <summary>
    /// Extracts sections and their content from the provided HTML string.
    /// </summary>
    /// <param name="html">The HTML content to extract sections from.</param>
    /// <returns>A dictionary where the key is the section title and the value is a list of content related to that section.</returns>
    private Dictionary<string, List<string>> ExtractSections(string html)
    {
        // Very simple HTML parsing - in a real app, use a proper HTML parser
        var result = new Dictionary<string, List<string>>();

        // Extract h3 elements as section headers
        const string h3Pattern = @"<h3>(.*?)<\/h3>";
        var h3Matches = InitializeHeaderRegex().Matches(html);

        for (var i = 0; i < h3Matches.Count; i++)
        {
            var sectionTitle = h3Matches[i].Groups[1].Value;
            var plugins = new List<string>();

            // Find all paragraphs after this h3 until the next h3
            var startIndex = h3Matches[i].Index + h3Matches[i].Length;
            var endIndex = (i < h3Matches.Count - 1) ? h3Matches[i + 1].Index : html.Length;

            var sectionContent = html.Substring(startIndex, endIndex - startIndex);

            // Extract plugin entries from paragraphs
            var pMatches = CreateParagraphRegex().Matches(sectionContent);

            foreach (Match pMatch in pMatches)
            {
                plugins.Add(pMatch.Groups[1].Value);
            }

            result[sectionTitle] = plugins;
        }

        return result;
    }

    /// <summary>
    /// Formats a section header by surrounding it with decorative equals signs.
    /// </summary>
    /// <param name="title">The title of the section to format.</param>
    /// <returns>A formatted section header string with decorative equals signs.</returns>
    private string FormatSectionHeader(string title)
    {
        if (title.Length >= 32) return $"\n   {title}\n";
        var diff = 32 - title.Length;
        var left = diff / 2;
        var right = diff - left;
        return $"\n   {'='.ToString().PadLeft(left, '=')} {title} {'='.ToString().PadRight(right, '=')}\n";
    }

    /// <summary>
    /// Scans for configuration files within a specified directory and organizes them based on provided parameters.
    /// </summary>
    /// <param name="rootPath">The root directory to start scanning for configuration files.</param>
    /// <param name="configFiles">A dictionary to store unique configuration files found during the scan, where the key is the file name and the value is the file path.</param>
    /// <param name="duplicateFiles">A dictionary to store duplicate configuration files, where the key is the file name and the value is a list of duplicate file paths.</param>
    /// <param name="whiteList">A list of folder or file name patterns to include in the scanning process.</param>
    private void ScanForConfigFiles(
        string rootPath,
        Dictionary<string, string> configFiles,
        Dictionary<string, List<string>> duplicateFiles,
        List<string> whiteList)
    {
        foreach (var file in Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file).ToLower();
            var directory = Path.GetDirectoryName(file);

            // Skip if directory doesn't contain any whitelist items and filename doesn't contain whitelist items
            if (directory != null &&
                !whiteList.Any(w => directory.Contains(w, StringComparison.OrdinalIgnoreCase)) &&
                !whiteList.Any(w => fileName.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Skip non-config files
            if (!fileName.EndsWith(".ini") && !fileName.EndsWith(".conf") && fileName != "dxvk.conf")
            {
                continue;
            }

            // Check for duplicates
            if (configFiles.TryGetValue(fileName, out var existingFile))
            {
                // Compare files for similarity
                if (!AreIniFilesSimilar(existingFile, file)) continue;
                // Add to duplicates
                if (!duplicateFiles.TryGetValue(fileName, out var value))
                {
                    value = [existingFile];
                    duplicateFiles[fileName] = value;
                }

                value.Add(file);
            }
            else
            {
                // Register new config file
                configFiles[fileName] = file;
            }
        }
    }

    /// <summary>
    /// Determines whether two INI files are similar by comparing their content, structure, or file attributes
    /// </summary>
    /// <param name="file1">The path to the first INI file</param>
    /// <param name="file2">The path to the second INI file</param>
    /// <returns>True if the files are deemed similar, false otherwise</returns>
    private bool AreIniFilesSimilar(string file1, string file2)
    {
        try
        {
            if (Path.GetExtension(file1).Equals(".ini", StringComparison.OrdinalIgnoreCase) &&
                Path.GetExtension(file2).Equals(".ini", StringComparison.OrdinalIgnoreCase))
            {
                var config1 = new Dictionary<string, Dictionary<string, string>>();
                var config2 = new Dictionary<string, Dictionary<string, string>>();

                LoadIniFile(file1, config1);
                LoadIniFile(file2, config2);

                // Compare sections
                if (!config1.Keys.SequenceEqual(config2.Keys))
                {
                    return false;
                }

                // Compare section contents
                foreach (var section in config1.Keys)
                {
                    if (!config1[section].Keys.SequenceEqual(config2[section].Keys))
                    {
                        return false;
                    }

                    if (config1[section].Keys.Any(key => config1[section][key] != config2[section][key]))
                    {
                        return false;
                    }
                }

                return true;
            }

            // For non-INI files, compare file size and modification time
            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            return info1.Length == info2.Length && info1.LastWriteTime == info2.LastWriteTime;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error comparing INI files: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads an INI file and parses its content into a dictionary structure.
    /// </summary>
    /// <param name="filePath">The path to the INI file to load.</param>
    /// <param name="config">The dictionary structure where the parsed INI file content will be stored.</param>
    private void LoadIniFile(string filePath, Dictionary<string, Dictionary<string, string>> config)
    {
        var currentSection = "";

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmedLine = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";"))
            {
                continue;
            }

            // Check for section
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                if (!config.ContainsKey(currentSection))
                {
                    config[currentSection] = new Dictionary<string, string>();
                }
            }
            // Process key-value pair
            else if (trimmedLine.Contains('='))
            {
                var parts = trimmedLine.Split(['='], 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Ensure we have a section
                if (string.IsNullOrEmpty(currentSection))
                {
                    currentSection = "General";
                    if (!config.ContainsKey(currentSection))
                    {
                        config[currentSection] = new Dictionary<string, string>();
                    }
                }

                config[currentSection][key] = value;
            }
        }
    }

    /// <summary>
    /// Checks for the presence of console command settings in configuration files that may impact game startup performance.
    /// </summary>
    /// <param name="configFiles">A dictionary mapping configuration file names to their file paths.</param>
    /// <param name="messageList">A list to append messages or notices about detected console command settings.</param>
    private void CheckConsoleCommandSettings(
        Dictionary<string, string> configFiles,
        List<string> messageList)
    {
        const string consoleCommandSetting = "sStartingConsoleCommand";
        const string consoleCommandSection = "General";
        const string consoleCommandNotice =
            "In rare cases, this setting can slow down the initial game startup time for some players.\n" +
            "You can test your initial startup time difference by removing this setting from the INI file.\n-----\n";

        var gameLower = GetCurrentGame().ToLower();

        foreach (var (fileName, filePath) in configFiles)
        {
            if (fileName.StartsWith(gameLower)) continue;
            // Load the config
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(filePath, config);

            // Check for console command setting
            if (!config.TryGetValue(consoleCommandSection, out var value) ||
                !value.ContainsKey(consoleCommandSetting)) continue;
            messageList.Add($"[!] NOTICE: {filePath} contains the *{consoleCommandSetting}* setting.\n");
            messageList.Add(consoleCommandNotice);
        }
    }

    /// <summary>
    /// Checks for VSync settings within the provided configuration files.
    /// </summary>
    /// <param name="configFiles">A dictionary where the key represents the file name and the value represents the file's path.</param>
    /// <returns>A list of detected VSync configurations.</returns>
    private List<string> CheckVSyncSettings(Dictionary<string, string> configFiles)
    {
        var vsyncList = new List<string>();

        // Define VSync settings to check
        var vsyncSettings = new[]
        {
            ("dxvk.conf", $"{GetCurrentGame()}.exe", "dxgi.syncInterval"),
            ("enblocal.ini", "ENGINE", "ForceVSync"),
            ("longloadingtimesfix.ini", "Limiter", "EnableVSync"),
            ("reshade.ini", "APP", "ForceVsync"),
            ("fallout4_test.ini", "CreationKit", "VSyncRender")
        };

        // Check standard VSync settings
        foreach (var (fileName, section, key) in vsyncSettings)
        {
            if (!configFiles.ContainsKey(fileName.ToLower())) continue;
            var filePath = configFiles[fileName.ToLower()];
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(filePath, config);

            if (config.TryGetValue(section, out var value) && value.TryGetValue(key, out var value2) &&
                value2.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                vsyncList.Add($"{filePath} | SETTING: {key}\n");
            }
        }

        // Check highfpsphysicsfix.ini separately
        if (!configFiles.TryGetValue("highfpsphysicsfix.ini", out var hfpfPath)) return vsyncList;
        {
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(hfpfPath, config);

            if (config.ContainsKey("Main") && config["Main"].ContainsKey("EnableVSync") &&
                config["Main"]["EnableVSync"].Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                vsyncList.Add($"{hfpfPath} | SETTING: EnableVSync\n");
            }
        }

        return vsyncList;
    }

    /// <summary>
    /// Applies fixes to the specified INI configuration files to ensure proper settings and functionality.
    /// </summary>
    /// <param name="configFiles">A dictionary where keys are the names of configuration files and values are their respective file paths.</param>
    /// <param name="messageList">A list to which informational or error messages related to the applied fixes will be added.</param>
    private void ApplyIniFixes(
        Dictionary<string, string> configFiles,
        List<string> messageList)
    {
        // Fix ESPExplorer hotkey if needed
        if (configFiles.ContainsKey("espexplorer.ini"))
        {
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(configFiles["espexplorer.ini"], config);

            if (config.ContainsKey("General") && config["General"].ContainsKey("HotKey") &&
                config["General"]["HotKey"].Contains("; F10"))
            {
                ApplyIniFix(configFiles["espexplorer.ini"], "General", "HotKey", "0x79", "INI HOTKEY", messageList);
            }
        }

        // Fix EPO particle count if needed
        if (configFiles.ContainsKey("epo.ini"))
        {
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(configFiles["epo.ini"], config);

            if (config.ContainsKey("Particles") && config["Particles"].ContainsKey("iMaxDesired") &&
                int.TryParse(config["Particles"]["iMaxDesired"], out int maxParticles) && maxParticles > 5000)
            {
                ApplyIniFix(configFiles["epo.ini"], "Particles", "iMaxDesired", "5000", "INI PARTICLE COUNT",
                    messageList);
            }
        }

        // Fix F4EE settings if present
        if (configFiles.ContainsKey("f4ee.ini"))
        {
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(configFiles["f4ee.ini"], config);

            // Fix head parts unlock setting
            if (config.ContainsKey("CharGen") && config["CharGen"].ContainsKey("bUnlockHeadParts") &&
                config["CharGen"]["bUnlockHeadParts"] == "0")
            {
                ApplyIniFix(configFiles["f4ee.ini"], "CharGen", "bUnlockHeadParts", "1", "INI HEAD PARTS UNLOCK",
                    messageList);
            }

            // Fix face tints unlock setting
            if (config.ContainsKey("CharGen") && config["CharGen"].ContainsKey("bUnlockTints") &&
                config["CharGen"]["bUnlockTints"] == "0")
            {
                ApplyIniFix(configFiles["f4ee.ini"], "CharGen", "bUnlockTints", "1", "INI FACE TINTS UNLOCK",
                    messageList);
            }
        }

        // Fix highfpsphysicsfix.ini loading screen FPS if present
        if (configFiles.ContainsKey("highfpsphysicsfix.ini"))
        {
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(configFiles["highfpsphysicsfix.ini"], config);

            if (config.ContainsKey("Limiter") && config["Limiter"].ContainsKey("LoadingScreenFPS") &&
                float.TryParse(config["Limiter"]["LoadingScreenFPS"], out float loadingScreenFps) &&
                loadingScreenFps < 600.0f)
            {
                ApplyIniFix(configFiles["highfpsphysicsfix.ini"], "Limiter", "LoadingScreenFPS", "600.0",
                    "INI LOADING SCREEN FPS", messageList);
            }
        }
    }

    /// <summary>
    /// Applies a specified fix to an INI configuration file by updating a specific section and setting with a new value,
    /// and logs the fix description to a message list.
    /// </summary>
    /// <param name="filePath">The path to the INI configuration file to be modified.</param>
    /// <param name="section">The section in the INI file where the setting is to be updated or added.</param>
    /// <param name="setting">The name of the setting to be updated or added.</param>
    /// <param name="value">The new value to be assigned to the specified setting.</param>
    /// <param name="fixDescription">A brief description of the fix being applied.</param>
    /// <param name="messageList">The list for storing messages about the applied fixes.</param>
    private void ApplyIniFix(
        string filePath,
        string section,
        string setting,
        string value,
        string fixDescription,
        List<string> messageList)
    {
        try
        {
            var config = new Dictionary<string, Dictionary<string, string>>();
            LoadIniFile(filePath, config);

            // Update the value
            if (!config.ContainsKey(section))
            {
                config[section] = new Dictionary<string, string>();
            }

            config[section][setting] = value;

            // Write back to file
            var lines = new List<string>();

            foreach (var sec in config)
            {
                lines.Add($"[{sec.Key}]");

                lines.AddRange(sec.Value.Select(entry => $"{entry.Key}={entry.Value}"));

                lines.Add(""); // Add a blank line between sections
            }

            File.WriteAllLines(filePath, lines);
            Console.WriteLine($"> > > PERFORMED {fixDescription} FIX FOR {filePath}");
            messageList.Add($"> Performed {fixDescription.ToUpper()} Fix For : {filePath}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying INI fix: {ex.Message}");
        }
    }

    [GeneratedRegex(@"<p>•&nbsp;\s*(.*?\.es[pm|l].*?)<\/p>")]
    private static partial Regex CreateParagraphRegex();

    [GeneratedRegex(@"<h3>(.*?)<\/h3>")]
    private static partial Regex InitializeHeaderRegex();
}