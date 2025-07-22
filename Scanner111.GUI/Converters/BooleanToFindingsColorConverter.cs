using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Scanner111.GUI.Converters;

/// <summary>
/// Converts a boolean value indicating findings into a corresponding color representation.
/// </summary>
public class BooleanToFindingsColorConverter : IValueConverter
{
    public static readonly BooleanToFindingsColorConverter Instance = new();

    /// <summary>
    /// Converts a boolean value into a color representation based on specific conditions.
    /// </summary>
    /// <param name="value">The boolean input value to be converted.</param>
    /// <param name="targetType">The target type of the conversion. Not used in this implementation.</param>
    /// <param name="parameter">A parameter for the conversion. Not used in this implementation.</param>
    /// <param name="culture">The culture to use in the conversion process. Not used in this implementation.</param>
    /// <returns>A string representation of a color. Returns orange (#FFFF9500) if the input is true, green (#FF4CAF50) if false, and gray (#FF666666) if the input is not a valid boolean.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasFindings)
            return hasFindings ? "#FFFF9500" : "#FF4CAF50"; // Orange for issues, Green for no issues
        return "#FF666666"; // Gray for unknown
    }

    /// <summary>
    /// Converts a color representation back to its equivalent boolean value. This method is currently not implemented.
    /// </summary>
    /// <param name="value">The color representation to be converted back. Not used in this implementation.</param>
    /// <param name="targetType">The target type of the conversion. Not used in this implementation.</param>
    /// <param name="parameter">A parameter for the conversion process. Not used in this implementation.</param>
    /// <param name="culture">The culture to use in the conversion process. Not used in this implementation.</param>
    /// <returns>Throws a NotImplementedException as this method is not implemented.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}