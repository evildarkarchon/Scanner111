using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scanner111.ClassicLib.ScanLog.Services;

/// <summary>
/// Utility service for crash log file operations.
/// </summary>
public interface ICrashLogFileService
{
    /// <summary>
    /// Gets all crash log files from the appropriate directories.
    /// </summary>
    /// <returns>A list of crash log file paths.</returns>
    Task<List<string>> GetCrashLogFilesAsync();

    /// <summary>
    /// Reformats crash log files according to settings.
    /// </summary>
    /// <param name="crashLogFiles">List of crash log files to reformat.</param>
    /// <param name="removePatterns">Patterns of lines to remove.</param>
    /// <returns>A task representing the reformatting operation.</returns>
    Task ReformatCrashLogsAsync(IEnumerable<string> crashLogFiles, IEnumerable<string> removePatterns);

    /// <summary>
    /// Moves files from source to target directory if they don't exist in target.
    /// </summary>
    /// <param name="sourceDir">Source directory.</param>
    /// <param name="targetDir">Target directory.</param>
    /// <param name="pattern">File pattern to match.</param>
    /// <returns>A task representing the move operation.</returns>
    Task MoveFilesAsync(string sourceDir, string targetDir, string pattern);

    /// <summary>
    /// Copies files from source to target directory if they don't exist in target.
    /// </summary>
    /// <param name="sourceDir">Source directory.</param>
    /// <param name="targetDir">Target directory.</param>
    /// <param name="pattern">File pattern to match.</param>
    /// <returns>A task representing the copy operation.</returns>
    Task CopyFilesAsync(string sourceDir, string targetDir, string pattern);
}

/// <summary>
/// Implementation of crash log file service.
/// </summary>
public class CrashLogFileService : ICrashLogFileService
{
    private const string CrashLogPattern = "crash-*.log";
    private const string CrashAutoscanPattern = "crash-*-AUTOSCAN.md";

    /// <summary>
    /// Gets all crash log files from various directories.
    /// </summary>
    public async Task<List<string>> GetCrashLogFilesAsync()
    {
        var baseFolder = Directory.GetCurrentDirectory();
        var crashLogsDir = Path.Combine(baseFolder, "Crash Logs");
        var pastebinDir = Path.Combine(crashLogsDir, "Pastebin");

        // Ensure directories exist
        Directory.CreateDirectory(crashLogsDir);
        Directory.CreateDirectory(pastebinDir);

        // Move files from base directory
        await MoveFilesAsync(baseFolder, crashLogsDir, CrashLogPattern);
        await MoveFilesAsync(baseFolder, crashLogsDir, CrashAutoscanPattern);

        // TODO: Add support for custom folders and XSE folders from settings
        // This would require injecting the settings service

        // Collect crash log files
        var crashFiles = new List<string>();
        crashFiles.AddRange(Directory.GetFiles(crashLogsDir, CrashLogPattern, SearchOption.AllDirectories));

        return crashFiles;
    }

    /// <summary>
    /// Reformats crash log files based on settings.
    /// </summary>
    public async Task ReformatCrashLogsAsync(IEnumerable<string> crashLogFiles, IEnumerable<string> removePatterns)
    {
        var removePatternsList = new List<string>(removePatterns);

        foreach (var file in crashLogFiles)
        {
            await ReformatSingleLogFileAsync(file, removePatternsList);
        }
    }

    /// <summary>
    /// Moves files matching pattern from source to target directory.
    /// </summary>
    public async Task MoveFilesAsync(string sourceDir, string targetDir, string pattern)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(sourceDir)) return;

            var files = Directory.GetFiles(sourceDir, pattern);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destinationFile = Path.Combine(targetDir, fileName);

                if (!File.Exists(destinationFile))
                {
                    try
                    {
                        File.Move(file, destinationFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error moving {file}: {ex.Message}");
                    }
                }
            }
        });
    }

    /// <summary>
    /// Copies files matching pattern from source to target directory.
    /// </summary>
    public async Task CopyFilesAsync(string sourceDir, string targetDir, string pattern)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(sourceDir)) return;

            var files = Directory.GetFiles(sourceDir, pattern);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destinationFile = Path.Combine(targetDir, fileName);

                if (!File.Exists(destinationFile))
                {
                    try
                    {
                        File.Copy(file, destinationFile, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error copying {file}: {ex.Message}");
                    }
                }
            }
        });
    }

    /// <summary>
    /// Reformats a single log file.
    /// </summary>
    private async Task ReformatSingleLogFileAsync(string filePath, List<string> removePatterns)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            var processedLines = new List<string>();
            var inPluginsSection = true;

            // Process lines from bottom to top (reverse order)
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];

                if (inPluginsSection && line.StartsWith("PLUGINS:"))
                {
                    inPluginsSection = false;
                }

                // Skip lines that match remove patterns
                if (removePatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Reformat plugin lines
                if (inPluginsSection && line.Contains('['))
                {
                    line = ReformatPluginLine(line);
                }

                processedLines.Add(line);
            }

            // Reverse the lines back to original order
            processedLines.Reverse();

            await File.WriteAllLinesAsync(filePath, processedLines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reformatting {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Reformats a plugin line by replacing spaces in brackets with zeros.
    /// </summary>
    private static string ReformatPluginLine(string line)
    {
        try
        {
            var bracketStart = line.IndexOf('[');
            var bracketEnd = line.IndexOf(']');

            if (bracketStart >= 0 && bracketEnd > bracketStart)
            {
                var prefix = line[..bracketStart];
                var bracket = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                var suffix = line[(bracketEnd + 1)..];

                var modifiedBracket = bracket.Replace(' ', '0');
                return $"{prefix}[{modifiedBracket}]{suffix}";
            }
        }
        catch
        {
            // If reformatting fails, return original line
        }

        return line;
    }
}
