using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class SubtractOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                int remaining = count - 1;
                return remaining > 0 ? remaining.ToString() : "0";
            }

            if (value is string str && int.TryParse(str, out int result))
            {
                int remaining = result - 1;
                return remaining > 0 ? remaining.ToString() : "0";
            }

            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}