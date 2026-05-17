using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Pages.Chat
{
    public class BoolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRegistered)
                return isRegistered ? "✉" : "📨"; // Message icon vs invite icon
            return "✉";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}