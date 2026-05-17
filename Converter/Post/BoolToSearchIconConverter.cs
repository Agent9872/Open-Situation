using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Post
{
    public class BoolToSearchIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool hasResults && hasResults ? "🔍" : "🔍";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}