using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.CLI.Test.Infrastructure;

public static class MockConsoleHelper
{
    public static TestConsole CreateTestConsole(int width = 80, int height = 24)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height;
        console.Profile.Capabilities.Interactive = true;
        console.Profile.Capabilities.Ansi = true;
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.Legacy = false;
        return console;
    }

    public static void SimulateKeyPress(TestConsole console, ConsoleKey key, bool shift = false, bool alt = false, bool control = false)
    {
        var modifiers = ConsoleModifiers.None;
        if (shift) modifiers |= ConsoleModifiers.Shift;
        if (alt) modifiers |= ConsoleModifiers.Alt;
        if (control) modifiers |= ConsoleModifiers.Control;

        console.Input.PushKey(new ConsoleKeyInfo((char)0, key, shift, alt, control));
    }

    public static void SimulateTextInput(TestConsole console, string text)
    {
        foreach (var ch in text)
        {
            console.Input.PushText(ch.ToString());
        }
    }

    public static string GetOutput(TestConsole console)
    {
        return console.Output;
    }

    public static List<string> GetOutputLines(TestConsole console)
    {
        return console.Output
            .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
            .ToList();
    }

    public static bool OutputContains(TestConsole console, string text)
    {
        return console.Output.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    public static void ClearOutput(TestConsole console)
    {
        console.Clear();
    }
}