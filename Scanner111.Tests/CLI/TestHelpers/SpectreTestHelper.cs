using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.Tests.CLI.TestHelpers;

/// <summary>
///     Helper class for testing Spectre.Console components
/// </summary>
public static class SpectreTestHelper
{
    /// <summary>
    ///     Creates a test console with specified capabilities
    /// </summary>
    public static TestConsole CreateTestConsole(
        int width = 80,
        int height = 24,
        bool supportsAnsi = true,
        bool interactive = true)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height;
        console.Profile.Capabilities.Ansi = supportsAnsi;
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.ColorSystem = ColorSystem.TrueColor;

        if (interactive) console.Interactive();

        return console;
    }

    /// <summary>
    ///     Removes ANSI escape codes from output for easier assertion
    /// </summary>
    public static string CleanOutput(string output)
    {
        return output
            .Replace("\u001b[", "")
            .Replace("[0m", "")
            .Replace("[38;5;", "")
            .Replace("[1m", "")
            .Replace("[2m", "")
            .Replace("[3m", "")
            .Replace("[4m", "")
            .Replace("[?25l", "")
            .Replace("[?25h", "")
            .Replace("[2J", "")
            .Replace("[H", "");
    }

    /// <summary>
    ///     Extracts text content from a TestConsole output
    /// </summary>
    public static List<string> ExtractLines(TestConsole console)
    {
        var output = console.Output;
        return output
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}