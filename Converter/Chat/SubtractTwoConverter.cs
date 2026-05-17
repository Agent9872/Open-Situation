using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class SubtractTwoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                int count = 0;

                if (value is int i) count = i;
                else if (value is string s && int.TryParse(s, out int parsed)) count = parsed;
                else if (value != null) count = System.Convert.ToInt32(value);

                // Only 1 image shown, remaining = total - 1
                int remaining = count - 1;
                return remaining > 0 ? remaining.ToString() : "0";
            }
            catch
            {
                return "0";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}