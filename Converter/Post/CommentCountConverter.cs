using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Post
{
    public class CommentCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                if (count == 0)
                    return "";
                if (count > 99)
                    return "99+";
                return count.ToString();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}