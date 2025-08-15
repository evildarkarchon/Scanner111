namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Default implementation of IConsoleService using System.Console
/// </summary>
public class ConsoleService : IConsoleService
{
    public string? ReadLine()
    {
        return Console.ReadLine();
    }

    public void Write(string text)
    {
        Console.Write(text);
    }

    public void WriteLine(string text)
    {
        Console.WriteLine(text);
    }

    public ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        return Console.ReadKey(intercept);
    }

    public bool KeyAvailable => Console.KeyAvailable;

    public void Clear()
    {
        Console.Clear();
    }
}