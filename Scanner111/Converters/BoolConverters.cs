using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Scanner111.Converters
{
    /// <summary>
    /// Converts boolean values to formatted strings.
    /// </summary>
    public class BoolToFormattedStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string format)
            {
                var parts = format.Split(';');
                if (parts.Length >= 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }

            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean values to Brushes for visual indication.
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string format)
            {
                var parts = format.Split(';');
                if (parts.Length >= 2)
                {
                    var colorName = boolValue ? parts[0] : parts[1];
                    if (colorName.StartsWith("#"))
                    {
                        return SolidColorBrush.Parse(colorName);
                    }
                    else
                    {
                        // Try to get the color by name
                        return new SolidColorBrush(Color.Parse(colorName));
                    }
                }
            }

            return new SolidColorBrush(Colors.Black); // Default
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
