using System.Globalization;

namespace Lock.Converter.Post
{
    public class LoveIconColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLoved)
            {
                return isLoved ? Color.FromArgb("#C05050") : Color.FromArgb("#888888");
            }
            return Color.FromArgb("#888888");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}