using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Scanner111.UI.Converters;

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string texts) return value?.ToString() ?? string.Empty;
        var textParts = texts.Split(',');
        if (textParts.Length != 2) return value?.ToString() ?? string.Empty;
        var trueText = textParts[0];
        var falseText = textParts[1];
                
        return boolValue ? trueText : falseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}