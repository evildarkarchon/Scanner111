using HtmlAgilityPack;
using Scanner111.Core.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.GameScanning;

/// <summary>
///     Analyzes Wrye Bash plugin checker reports for issues.
/// </summary>
public class WryeBashChecker : IWryeBashChecker
{
    private readonly ILogger<WryeBashChecker> _logger;
    private readonly List<string> _messageList = new();

    // Resource links for Wrye Bash
    private readonly Dictionary<string, string> _resourceLinks = new()
    {
        ["troubleshooting"] = "https://www.nexusmods.com/fallout4/articles/4141",
        ["documentation"] = "https://wrye-bash.github.io/docs/",
        ["simple_eslify"] = "https://www.nexusmods.com/skyrimspecialedition/mods/27568"
    };

    private readonly IApplicationSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlProvider;
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentPathProvider _environment;
    private readonly IPathService _pathService;

    public WryeBashChecker(
        IApplicationSettingsService settingsService,
        IYamlSettingsProvider yamlProvider,
        ILogger<WryeBashChecker> logger,
        IFileSystem fileSystem,
        IEnvironmentPathProvider environment,
        IPathService pathService)
    {
        _settingsService = settingsService;
        _yamlProvider = yamlProvider;
        _logger = logger;
        _fileSystem = fileSystem;
        _environment = environment;
        _pathService = pathService;
    }

    public async Task<string> AnalyzeAsync()
    {
        return await Task.Run(() =>
        {
            var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
            var gameType = settings.GameType;

            // Determine the path to the Wrye Bash plugin checker report
            var reportPath = GetPluginCheckerReportPath(gameType);

            if (string.IsNullOrEmpty(reportPath) || !_fileSystem.FileExists(reportPath)) return GetMissingReportMessage(gameType);

            // Build the analysis message
            var gameFolderName = GetGameFolderName(gameType);
            _messageList.AddRange(new[]
            {
                "\n✔️ WRYE BASH PLUGIN CHECKER REPORT WAS FOUND! ANALYZING CONTENTS...\n",
                $"  [This report is located in your Documents/My Games/{gameFolderName} folder.]\n",
                "  [To hide this report, remove *ModChecker.html* from the same folder.]\n"
            });

            // Parse the HTML report
            var reportContents = ParseWryeReport(reportPath);
            _messageList.AddRange(reportContents);

            // Add resource links
            _messageList.AddRange(new[]
            {
                "\n❔ For more info about the above detected problems, see the WB Advanced Readme\n",
                "  For more details about solutions, read the Advanced Troubleshooting Article\n",
                $"  Advanced Troubleshooting: {_resourceLinks["troubleshooting"]}\n",
                $"  Wrye Bash Advanced Readme Documentation: {_resourceLinks["documentation"]}\n",
                "  [ After resolving any problems, run Plugin Checker in Wrye Bash again! ]\n\n"
            });

            return string.Join("", _messageList);
        });
    }

    private string GetPluginCheckerReportPath(GameType gameType)
    {
        var documentsPath = _environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var gameFolderName = GetGameFolderName(gameType);

        if (string.IsNullOrEmpty(gameFolderName))
            return string.Empty;

        return _pathService.Combine(documentsPath, "My Games", gameFolderName, "ModChecker.html");
    }

    private string GetGameFolderName(GameType gameType)
    {
        return gameType switch
        {
            GameType.Fallout4 or GameType.Fallout4VR => "Fallout4",
            GameType.SkyrimSE => "Skyrim Special Edition",
            GameType.SkyrimVR => "Skyrim VR",
            GameType.Skyrim => "Skyrim",
            GameType.FalloutNV => "FalloutNV",
            GameType.Fallout3 => "Fallout3",
            _ => string.Empty
        };
    }

    private string GetMissingReportMessage(GameType gameType)
    {
        // TODO: Load warning message from YAML configuration when structure is defined
        // For now, use default message
        var gameFolderName = GetGameFolderName(gameType);
        var gameDisplayName = string.IsNullOrEmpty(gameFolderName) ? gameType.ToString() : gameFolderName;

        return $"ℹ️ Wrye Bash Plugin Checker Report for {gameDisplayName} was not found.\n" +
               "  Run Plugin Checker in Wrye Bash to generate this report.\n" +
               "  The report helps identify plugin conflicts and load order issues.\n-----\n";
    }

    private List<string> ParseWryeReport(string reportPath)
    {
        var messageParts = new List<string>();
        var hasIssues = false;

        try
        {
            var htmlContent = _fileSystem.ReadAllText(reportPath);
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Check for Missing Masters
            var missingMastersSection = doc.DocumentNode.SelectSingleNode("//h2[contains(text(), 'Missing Masters')]");
            if (missingMastersSection != null)
            {
                hasIssues = true;
                messageParts.Add("\n❌ CRITICAL: Missing Masters detected\n");
                var items = ExtractItemsFromSection(missingMastersSection);
                foreach (var item in items)
                    messageParts.Add($"  - {item}\n");
            }

            // Check for Deactivated Plugins
            var deactivatedSection = doc.DocumentNode.SelectSingleNode("//h2[contains(text(), 'Deactivated Plugins')]");
            if (deactivatedSection != null)
            {
                hasIssues = true;
                messageParts.Add("\n⚠️ WARNING: Deactivated plugins found\n");
                var items = ExtractItemsFromSection(deactivatedSection);
                foreach (var item in items)
                    messageParts.Add($"  - {item}\n");
            }

            // Check for Load Order Issues
            var loadOrderSection = doc.DocumentNode.SelectSingleNode("//h2[contains(text(), 'Load Order Issues')]");
            if (loadOrderSection != null)
            {
                hasIssues = true;
                messageParts.Add("\n⚠️ WARNING: Load order issues detected\n");
                var items = ExtractTextFromSection(loadOrderSection);
                foreach (var item in items)
                    messageParts.Add($"  - {item}\n");
            }

            // Check for Dirty Plugins
            var dirtyPluginsSection = doc.DocumentNode.SelectSingleNode("//h2[contains(text(), 'Dirty Plugins')]");
            if (dirtyPluginsSection != null)
            {
                hasIssues = true;
                messageParts.Add("\n⚠️ WARNING: Dirty plugins detected\n");
                var tableRows = ExtractTableRows(dirtyPluginsSection);
                foreach (var row in tableRows)
                    messageParts.Add($"  - {row}\n");
            }

            // Check for ESL Flag Issues
            var eslSection = doc.DocumentNode.SelectSingleNode("//h2[contains(text(), 'ESL Flag Issues')] | //h2[contains(text(), 'ESL Capable')]");
            if (eslSection != null)
            {
                messageParts.Add("\n⚠️ WARNING: ESL flag optimization available\n");
                var items = ExtractItemsFromSection(eslSection);
                foreach (var item in items)
                    messageParts.Add($"  - {item}\n");
            }

            // Check for Bash Tag Suggestions
            var bashTagSection = doc.DocumentNode.SelectSingleNode("//h2[contains(text(), 'Bash Tag')]");
            if (bashTagSection != null)
            {
                messageParts.Add("\nℹ️ INFO: Bash Tag suggestions\n");
                var items = ExtractItemsFromSection(bashTagSection);
                foreach (var item in items)
                    messageParts.Add($"  - {item}\n");
            }

            // If no issues were found
            if (!hasIssues && messageParts.Count == 0)
            {
                messageParts.Add("\n✔️ No issues detected in Wrye Bash report\n");
            }

            // Also check for h3 sections (fallback for different HTML structure)
            var h3Sections = doc.DocumentNode.SelectNodes("//h3");
            if (h3Sections != null && messageParts.Count == 1 && messageParts[0].Contains("No issues"))
            {
                messageParts.Clear();
                foreach (var section in h3Sections)
                {
                    var title = section.InnerText.Trim();
                    if (title != "Active Plugins:")
                    {
                        messageParts.Add(FormatSectionHeader(title));
                        var plugins = ExtractPluginsFromSection(section);
                        foreach (var plugin in plugins)
                            messageParts.Add($"    > {plugin}\n");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Wrye Bash report");
            messageParts.Add($"# ❌ ERROR : Failed to parse Wrye Bash report: {ex.Message} #\n-----\n");
        }

        return messageParts;
    }

    private List<string> ExtractPluginsFromSection(HtmlNode section)
    {
        var plugins = new List<string>();

        try
        {
            // Find all paragraph siblings after this h3 until the next h3
            var currentNode = section.NextSibling;

            while (currentNode != null)
            {
                // Stop if we hit another h3 section
                if (currentNode.Name == "h3")
                    break;

                // Process paragraph nodes
                if (currentNode.Name == "p")
                {
                    var text = currentNode.InnerText.Trim()
                        .Replace("•\u00a0", "") // Remove bullet point
                        .Replace("\u2022 ", "") // Alternative bullet format
                        .Trim();

                    // Check if it's a plugin file (has extension)
                    if (text.Contains(".esp") || text.Contains(".esl") || text.Contains(".esm")) plugins.Add(text);
                }

                currentNode = currentNode.NextSibling;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting plugins from section");
        }

        return plugins;
    }

    private List<string> ExtractItemsFromSection(HtmlNode section)
    {
        var items = new List<string>();

        try
        {
            // Find the next sibling that contains list items
            var currentNode = section.NextSibling;

            while (currentNode != null)
            {
                // Stop if we hit another h2 or h3 section
                if (currentNode.Name == "h2" || currentNode.Name == "h3")
                    break;

                // Process ul/ol nodes
                if (currentNode.Name == "ul" || currentNode.Name == "ol")
                {
                    var listItems = currentNode.SelectNodes(".//li");
                    if (listItems != null)
                    {
                        foreach (var li in listItems)
                        {
                            var text = li.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                                items.Add(text);
                        }
                    }
                }
                // Process paragraph nodes
                else if (currentNode.Name == "p")
                {
                    var text = currentNode.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 2)
                        items.Add(text);
                }

                currentNode = currentNode.NextSibling;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting items from section");
        }

        return items;
    }

    private List<string> ExtractTextFromSection(HtmlNode section)
    {
        var items = new List<string>();

        try
        {
            var currentNode = section.NextSibling;

            while (currentNode != null)
            {
                // Stop if we hit another h2 or h3 section
                if (currentNode.Name == "h2" || currentNode.Name == "h3")
                    break;

                // Process paragraph nodes
                if (currentNode.Name == "p")
                {
                    var text = currentNode.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        items.Add(text);
                }

                currentNode = currentNode.NextSibling;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting text from section");
        }

        return items;
    }

    private List<string> ExtractTableRows(HtmlNode section)
    {
        var rows = new List<string>();

        try
        {
            var currentNode = section.NextSibling;

            while (currentNode != null)
            {
                // Stop if we hit another h2 or h3 section
                if (currentNode.Name == "h2" || currentNode.Name == "h3")
                    break;

                // Process table nodes
                if (currentNode.Name == "table")
                {
                    var tableRows = currentNode.SelectNodes(".//tr");
                    if (tableRows != null)
                    {
                        var skipFirst = true;
                        foreach (var tr in tableRows)
                        {
                            // Skip header row if it has th elements
                            if (skipFirst && tr.SelectNodes(".//th") != null)
                            {
                                skipFirst = false;
                                continue;
                            }
                            
                            var cells = tr.SelectNodes(".//td");
                            if (cells != null && cells.Count >= 2)
                            {
                                var plugin = cells[0].InnerText.Trim();
                                var issues = cells[1].InnerText.Trim();
                                rows.Add($"{plugin}: {issues}");
                            }
                        }
                    }
                    break; // We found and processed the table, stop looking
                }

                currentNode = currentNode.NextSibling;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting table rows from section");
        }

        return rows;
    }

    private string FormatSectionHeader(string title)
    {
        if (title.Length < 32)
        {
            var diff = 32 - title.Length;
            var left = diff / 2;
            var right = diff - left;
            return $"\n   {new string('=', left)} {title} {new string('=', right)}\n";
        }

        return $"\n   === {title} ===\n";
    }
}