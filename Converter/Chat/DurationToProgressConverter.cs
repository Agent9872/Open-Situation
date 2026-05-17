using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class DurationToProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double currentPosition && parameter is double totalDuration && totalDuration > 0)
            {
                return currentPosition / totalDuration;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress && parameter is double totalDuration && totalDuration > 0)
            {
                return progress * totalDuration;
            }
            return 0;
        }
    }
}