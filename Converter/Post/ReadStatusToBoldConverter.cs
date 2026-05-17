using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Post
{
    public class ReadStatusToBoldConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRead)
            {
                return !isRead ? FontAttributes.Bold : FontAttributes.None;
            }
            return FontAttributes.None;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}