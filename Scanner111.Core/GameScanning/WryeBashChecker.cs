using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.GameScanning
{
    /// <summary>
    /// Analyzes Wrye Bash plugin checker reports for issues.
    /// </summary>
    public class WryeBashChecker : IWryeBashChecker
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly IYamlSettingsProvider _yamlProvider;
        private readonly ILogger<WryeBashChecker> _logger;
        private readonly List<string> _messageList = new();

        // Resource links for Wrye Bash
        private readonly Dictionary<string, string> _resourceLinks = new()
        {
            ["troubleshooting"] = "https://www.nexusmods.com/fallout4/articles/4141",
            ["documentation"] = "https://wrye-bash.github.io/docs/",
            ["simple_eslify"] = "https://www.nexusmods.com/skyrimspecialedition/mods/27568"
        };

        public WryeBashChecker(
            IApplicationSettingsService settingsService,
            IYamlSettingsProvider yamlProvider,
            ILogger<WryeBashChecker> logger)
        {
            _settingsService = settingsService;
            _yamlProvider = yamlProvider;
            _logger = logger;
        }

        public async Task<string> AnalyzeAsync()
        {
            return await Task.Run(() =>
            {
                var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
                var gameType = settings.GameType;
                
                // Determine the path to the Wrye Bash plugin checker report
                var reportPath = GetPluginCheckerReportPath(gameType);
                
                if (string.IsNullOrEmpty(reportPath) || !File.Exists(reportPath))
                {
                    return GetMissingReportMessage(gameType);
                }

                // Build the analysis message
                _messageList.AddRange(new[]
                {
                    "\n✔️ WRYE BASH PLUGIN CHECKER REPORT WAS FOUND! ANALYZING CONTENTS...\n",
                    $"  [This report is located in your Documents/My Games/{GetGameFolderName(gameType)} folder.]\n",
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
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var gameFolderName = GetGameFolderName(gameType);
            
            if (string.IsNullOrEmpty(gameFolderName))
                return string.Empty;
                
            return Path.Combine(documentsPath, "My Games", gameFolderName, "ModChecker.html");
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
            
            return $"ℹ️ Wrye Bash Plugin Checker report not found for {gameType}.\n" +
                   "  Run Plugin Checker in Wrye Bash to generate this report.\n" +
                   "  The report helps identify plugin conflicts and load order issues.\n-----\n";
        }

        private List<string> ParseWryeReport(string reportPath)
        {
            var messageParts = new List<string>();
            
            try
            {
                var htmlContent = File.ReadAllText(reportPath);
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // Get Wrye warnings from YAML settings
                // TODO: Load warnings from YAML configuration when structure is defined
                var wryeWarnings = new Dictionary<string, string>();

                // Process each section (h3 element)
                var sections = doc.DocumentNode.SelectNodes("//h3");
                if (sections != null)
                {
                    foreach (var section in sections)
                    {
                        var title = section.InnerText.Trim();
                        var plugins = ExtractPluginsFromSection(section);

                        // Format section header (skip Active Plugins section)
                        if (title != "Active Plugins:")
                        {
                            messageParts.Add(FormatSectionHeader(title));
                        }

                        // Handle special ESL Capable section
                        if (title == "ESL Capable")
                        {
                            messageParts.AddRange(new[]
                            {
                                $"❓ There are {plugins.Count} plugins that can be given the ESL flag. This can be done with\n",
                                "  the SimpleESLify script to avoid reaching the plugin limit (254 esm/esp).\n",
                                $"  SimpleESLify: {_resourceLinks["simple_eslify"]}\n  -----\n"
                            });
                        }

                        // Add any matching warnings from settings
                        foreach (var (warningName, warningText) in wryeWarnings)
                        {
                            if (title.Contains(warningName, StringComparison.OrdinalIgnoreCase))
                            {
                                messageParts.Add(warningText);
                            }
                        }

                        // List plugins (except for special sections)
                        if (title != "ESL Capable" && title != "Active Plugins:")
                        {
                            foreach (var plugin in plugins)
                            {
                                messageParts.Add($"    > {plugin}\n");
                            }
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
                            .Replace("\u2022 ", "")    // Alternative bullet format
                            .Trim();
                            
                        // Check if it's a plugin file (has extension)
                        if (text.Contains(".esp") || text.Contains(".esl") || text.Contains(".esm"))
                        {
                            plugins.Add(text);
                        }
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
}