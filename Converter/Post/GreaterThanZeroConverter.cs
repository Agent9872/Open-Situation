using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Post
{
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return intValue > 0;

            if (value is string strValue && int.TryParse(strValue, out int parsedValue))
                return parsedValue > 0;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}