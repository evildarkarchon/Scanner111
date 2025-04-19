using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Scanner111.UI.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string colors)
        {
            var colorParts = colors.Split(',');
            if (colorParts.Length == 2)
            {
                var trueColor = colorParts[0];
                var falseColor = colorParts[1];
                
                return SolidColorBrush.Parse(boolValue ? trueColor : falseColor);
            }
        }
        
        // Default fallback
        return SolidColorBrush.Parse("#000000");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}