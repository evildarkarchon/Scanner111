using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Services;

namespace Scanner111.ClassicLib.Papyrus.Services;

/// <summary>
///     Implementation of the service for analyzing Papyrus log files.
/// </summary>
public class PapyrusLogService : IPapyrusLogService
{
    private readonly IGameContextService _gameContextService;
    private readonly ILogger<PapyrusLogService> _logger;
    private readonly IYamlSettingsCache _yamlSettingsCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PapyrusLogService" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="yamlSettingsCache">The YAML settings cache service.</param>
    /// <param name="gameContextService">The game context service.</param>
    public PapyrusLogService(
        ILogger<PapyrusLogService> logger,
        IYamlSettingsCache yamlSettingsCache,
        IGameContextService gameContextService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
        _gameContextService = gameContextService ?? throw new ArgumentNullException(nameof(gameContextService));
    }

    /// <inheritdoc />
    public (string Message, int DumpCount) AnalyzePapyrusLog()
    {
        var messageList = new List<string>();

        // Get the current game and VR (equivalent to GlobalRegistry.get_vr() in Python)
        var gameVr = _gameContextService.GetCurrentGame();

        // Get Papyrus log path from YAML settings
        var papyrusPathSetting = $"Game{gameVr}_Info.Docs_File_PapyrusLog";
        var papyrusPath = _yamlSettingsCache.GetSetting<string>(YamlStore.GameLocal, papyrusPathSetting);

        var countDumps = 0;
        var countStacks = 0;
        var countWarnings = 0;
        var countErrors = 0;

        if (!string.IsNullOrEmpty(papyrusPath) && File.Exists(papyrusPath))
            try
            {
                // Read the file with encoding detection
                var papyrusData = ReadFileWithAutoEncoding(papyrusPath);

                // Analyze the log content
                foreach (var line in papyrusData)
                    if (line.Contains("Dumping Stacks"))
                        countDumps++;
                    else if (line.Contains("Dumping Stack"))
                        countStacks++;
                    else if (line.Contains(" warning: "))
                        countWarnings++;
                    else if (line.Contains(" error: ")) countErrors++;

                // Calculate ratio
                var ratio = countDumps == 0 ? 0.0f : (float)countDumps / countStacks;

                // Format the output message
                messageList.AddRange([
                    $"NUMBER OF DUMPS    : {countDumps}\n",
                    $"NUMBER OF STACKS   : {countStacks}\n",
                    $"DUMPS/STACKS RATIO : {Math.Round(ratio, 3)}\n",
                    $"NUMBER OF WARNINGS : {countWarnings}\n",
                    $"NUMBER OF ERRORS   : {countErrors}\n"
                ]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing Papyrus log file");
                messageList.Add($"[!] ERROR : Exception while reading Papyrus log file: {ex.Message}\n");
            }
        else
            // Log file not found - provide guidance
            messageList.AddRange([
                "[!] ERROR : UNABLE TO FIND *Papyrus.0.log* (LOGGING IS DISABLED OR YOU DIDN'T RUN THE GAME)\n",
                "ENABLE PAPYRUS LOGGING MANUALLY OR WITH BETHINI AND START THE GAME TO GENERATE THE LOG FILE\n",
                "BethINI Link | Use Manual Download : https://www.nexusmods.com/site/mods/631?tab=files\n"
            ]);

        var messageOutput = string.Concat(messageList);
        return (messageOutput, countDumps);
    }

    /// <summary>
    ///     Reads a file with automatic encoding detection.
    /// </summary>
    /// <param name="filePath">The path to the file to read.</param>
    /// <returns>An array of strings representing the lines in the file.</returns>
    private string[] ReadFileWithAutoEncoding(string filePath)
    {
        // Read a small portion of the file to detect encoding
        var buffer = new byte[Math.Min(4096, new FileInfo(filePath).Length)];
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            var bytesRead = 0;
            var totalBytesRead = 0;
            while (totalBytesRead < buffer.Length &&
                   (bytesRead = fs.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead)) > 0)
                totalBytesRead += bytesRead;
        }

        // Try to detect encoding
        var encoding = DetectEncoding(buffer);

        // Read the entire file with the detected encoding
        return File.ReadAllLines(filePath, encoding);
    }

    /// <summary>
    ///     Detects the encoding of a byte array.
    /// </summary>
    /// <param name="buffer">The byte array to analyze.</param>
    /// <returns>The detected encoding, or null if detection fails.</returns>
    private Encoding DetectEncoding(byte[] buffer)
    {
        // Check for BOM (Byte Order Mark)
        if (buffer.Length >= 2)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode;
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (buffer is [0xEF, 0xBB, 0xBF, ..])
                return Encoding.UTF8;
            if (buffer is [0, 0, 0xFE, 0xFF, ..])
                return Encoding.UTF32;
        }

        // Simple heuristic for ASCII vs UTF-8
        // More sophisticated detection would require a dedicated library
        var isProbablyUtf8 = true;
        var hasNonAscii = false;

        var i = 0;
        while (i < buffer.Length)
        {
            if (buffer[i] <= 0x7F) // ASCII range
            {
                i++;
                continue;
            }

            hasNonAscii = true;

            // Check UTF-8 pattern
            if (buffer[i] >= 0xC2 && buffer[i] <= 0xDF) // 2-byte sequence
            {
                if (i + 1 < buffer.Length && (buffer[i + 1] & 0xC0) == 0x80)
                {
                    i += 2;
                }
                else
                {
                    isProbablyUtf8 = false;
                    break;
                }
            }
            else if (buffer[i] >= 0xE0 && buffer[i] <= 0xEF) // 3-byte sequence
            {
                if (i + 2 < buffer.Length && (buffer[i + 1] & 0xC0) == 0x80 && (buffer[i + 2] & 0xC0) == 0x80)
                {
                    i += 3;
                }
                else
                {
                    isProbablyUtf8 = false;
                    break;
                }
            }
            else if (buffer[i] >= 0xF0 && buffer[i] <= 0xF7) // 4-byte sequence
            {
                if (i + 3 < buffer.Length && (buffer[i + 1] & 0xC0) == 0x80 &&
                    (buffer[i + 2] & 0xC0) == 0x80 && (buffer[i + 3] & 0xC0) == 0x80)
                {
                    i += 4;
                }
                else
                {
                    isProbablyUtf8 = false;
                    break;
                }
            }
            else
            {
                isProbablyUtf8 = false;
                break;
            }
        }

        if (hasNonAscii) return isProbablyUtf8 ? Encoding.UTF8 : Encoding.Default;

        // If all characters are ASCII, return ASCII encoding
        return Encoding.ASCII;
    }
}