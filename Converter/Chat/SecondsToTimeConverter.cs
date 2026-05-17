using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class SecondsToTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int seconds)
            {
                var timeSpan = TimeSpan.FromSeconds(seconds);
                return timeSpan.ToString(@"mm\:ss");
            }

            if (value is double secondsDouble)
            {
                var timeSpan = TimeSpan.FromSeconds(secondsDouble);
                return timeSpan.ToString(@"mm\:ss");
            }

            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}