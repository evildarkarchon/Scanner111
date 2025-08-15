namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Abstraction for console operations to enable testability
/// </summary>
public interface IConsoleService
{
    /// <summary>
    ///     Check if a key is available to read
    /// </summary>
    bool KeyAvailable { get; }

    /// <summary>
    ///     Read a line from the console
    /// </summary>
    string? ReadLine();

    /// <summary>
    ///     Write text to the console
    /// </summary>
    void Write(string text);

    /// <summary>
    ///     Write a line to the console
    /// </summary>
    void WriteLine(string text);

    /// <summary>
    ///     Read a key from the console
    /// </summary>
    ConsoleKeyInfo ReadKey(bool intercept = false);

    /// <summary>
    ///     Clear the console
    /// </summary>
    void Clear();
}