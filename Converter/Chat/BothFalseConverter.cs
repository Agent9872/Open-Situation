using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class BothFalseConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is bool val1 && values[1] is bool val2)
                return !val1 && !val2;
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}