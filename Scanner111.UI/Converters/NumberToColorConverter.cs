using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Scanner111.UI.Converters;

public class NumberToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string colors)
        {
            var colorParts = colors.Split(',');
            if (colorParts.Length == 2)
            {
                var lowValueColor = colorParts[0];
                var highValueColor = colorParts[1];
                
                return intValue == 0 
                    ? SolidColorBrush.Parse(lowValueColor) 
                    : SolidColorBrush.Parse(highValueColor);
            }
        }
        return SolidColorBrush.Parse("#000000");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}