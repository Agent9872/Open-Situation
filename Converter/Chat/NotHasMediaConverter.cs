using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class NotHasMediaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            // If it's a message object, you might want to check HasMedia property
            // For now, just return the inverse of the value
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}