using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Scanner111.GUI.Converters;

/// <summary>
/// Converts a boolean value indicating findings into an appropriate text description.
/// </summary>
public class BooleanToFindingsTextConverter : IValueConverter
{
    public static readonly BooleanToFindingsTextConverter Instance = new();

    /// <summary>
    /// Converts a boolean value to a text representation indicating findings.
    /// </summary>
    /// <param name="value">The boolean value to be converted.</param>
    /// <param name="targetType">The target type of the conversion operation.</param>
    /// <param name="parameter">An optional parameter to influence the conversion.</param>
    /// <param name="culture">The culture to use during conversion.</param>
    /// <returns>
    /// Returns "Issues Found" if the input value is true, "No Issues" if the value is false,
    /// or "Unknown" if the input value is not a boolean.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasFindings) return hasFindings ? "Issues Found" : "No Issues";
        return "Unknown";
    }

    /// <summary>
    /// Converts a text representation of findings back into a boolean value.
    /// </summary>
    /// <param name="value">The text value to be converted back.</param>
    /// <param name="targetType">The target type of the conversion operation.</param>
    /// <param name="parameter">An optional parameter to influence the conversion.</param>
    /// <param name="culture">The culture to use during conversion.</param>
    /// <returns>
    /// Throws a <see cref="NotImplementedException"/> as the conversion back has not been implemented.
    /// </returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}