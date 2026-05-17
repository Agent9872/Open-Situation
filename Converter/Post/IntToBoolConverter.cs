using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Post
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && parameter is string compare)
            {
                if (compare == "2")
                    return count >= 2;
                if (compare == "3")
                    return count >= 3;
                if (compare == "1")
                    return count >= 1;
                if (int.TryParse(compare, out int threshold))
                    return count >= threshold;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}