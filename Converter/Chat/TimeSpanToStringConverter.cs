using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalHours >= 1)
                    return timeSpan.ToString(@"h\:mm\:ss");
                else
                    return timeSpan.ToString(@"m\:ss");
            }

            if (value is int seconds)
            {
                var ts = TimeSpan.FromSeconds(seconds);
                if (ts.TotalHours >= 1)
                    return ts.ToString(@"h\:mm\:ss");
                else
                    return ts.ToString(@"m\:ss");
            }

            if (value is double secondsDouble)
            {
                var ts = TimeSpan.FromSeconds(secondsDouble);
                if (ts.TotalHours >= 1)
                    return ts.ToString(@"h\:mm\:ss");
                else
                    return ts.ToString(@"m\:ss");
            }

            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}