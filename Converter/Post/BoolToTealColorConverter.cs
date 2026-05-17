using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Lock.Converter.Post
{
    public class BoolToTealColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRecent && isRecent)
            {
                return Color.FromArgb("#008080"); // Teal color for recent updates
            }
            return Color.FromArgb("#888888"); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}