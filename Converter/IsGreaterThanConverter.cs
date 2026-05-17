using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lock.Converter
{
    // IsGreaterThanConverter
    public class IsGreaterThanConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out int paramInt))
                return intValue > paramInt;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => false;

        public object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
