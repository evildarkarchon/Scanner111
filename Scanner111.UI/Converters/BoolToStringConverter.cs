using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Scanner111.UI.Converters;

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string texts)
        {
            var textParts = texts.Split(',');
            if (textParts.Length == 2)
            {
                var trueText = textParts[0];
                var falseText = textParts[1];
                
                return boolValue ? trueText : falseText;
            }
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}