using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Scanner111.UI.Converters;

public class NumberToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int intValue || parameter is not string colors) return SolidColorBrush.Parse("#000000");
        var colorParts = colors.Split(',');
        if (colorParts.Length != 2) return SolidColorBrush.Parse("#000000");
        var lowValueColor = colorParts[0];
        var highValueColor = colorParts[1];
                
        return intValue == 0 
            ? SolidColorBrush.Parse(lowValueColor) 
            : SolidColorBrush.Parse(highValueColor);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}