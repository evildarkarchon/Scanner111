using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Scanner111.GUI.Converters;

/// <summary>
/// Converts boolean findings value to appropriate display text
/// </summary>
public class BooleanToFindingsTextConverter : IValueConverter
{
    public static readonly BooleanToFindingsTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasFindings)
        {
            return hasFindings ? "Issues Found" : "No Issues";
        }
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}