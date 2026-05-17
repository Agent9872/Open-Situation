using System.Globalization;

namespace Lock.Converter.Chat
{
    public class TabColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? Color.FromArgb("#FF3B30") : Color.FromArgb("#AAAAAA");
            }
            return Color.FromArgb("#AAAAAA");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}