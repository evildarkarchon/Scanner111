namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Provides guard clause utilities for argument validation.
/// </summary>
public static class Guard
{
    /// <summary>
    ///     Validates that the specified argument is not null.
    /// </summary>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <param name="value">The argument value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated.</param>
    /// <returns>The validated argument value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    public static T NotNull<T>(T value, string paramName) where T : class
    {
        return value ?? throw new ArgumentNullException(paramName);
    }

    /// <summary>
    ///     Validates that the specified string argument is not null or empty.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated.</param>
    /// <returns>The validated string value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is empty.</exception>
    public static string NotNullOrEmpty(string value, string paramName)
    {
        if (value == null)
            throw new ArgumentNullException(paramName);
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be empty.", paramName);
        return value;
    }

    /// <summary>
    ///     Validates that the specified string argument is not null, empty, or whitespace.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated.</param>
    /// <returns>The validated string value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is empty or whitespace.</exception>
    public static string NotNullOrWhiteSpace(string value, string paramName)
    {
        if (value == null)
            throw new ArgumentNullException(paramName);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
        return value;
    }
}