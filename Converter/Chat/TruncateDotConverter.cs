using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converters
{
    public class TruncateDotConverter : IValueConverter
    {
        // parameter: maximum characters (optional). Default = 48.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string ?? string.Empty;
            int max = 48;
            if (parameter is string ps && int.TryParse(ps, out var p)) max = p;
            if (text.Length <= max) return text;
            // return truncated text with single middle dot indicating continuation
            var visible = text.Substring(0, Math.Max(0, max - 1));
            return visible + "·";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}