using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Scanner111.GUI.Converters;

/// <summary>
///     Converts boolean findings value to appropriate color
/// </summary>
public class BooleanToFindingsColorConverter : IValueConverter
{
    public static readonly BooleanToFindingsColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasFindings)
            return hasFindings ? "#FFFF9500" : "#FF4CAF50"; // Orange for issues, Green for no issues
        return "#FF666666"; // Gray for unknown
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}