using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Lock.Converter.Post
{
    // List Not Empty Converter
    // Bool to Red Color Converter (for smokes icon)
    public class BoolToRedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Color.FromArgb("#FF4444") : Color.FromArgb("#444444");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Bool to Teal Color Converter (for pets icon)
    public class BoolToTealConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Color.FromArgb("#008080") : Color.FromArgb("#444444");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Drink to Color Converter
    public class DrinkColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var drink = value as string;
            if (string.IsNullOrEmpty(drink))
                return Color.FromArgb("#888880");

            if (drink.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb("#FF6B6B");
            if (drink.Equals("Socially", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb("#FFD93D");

            return Color.FromArgb("#888880");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Invert Bool Converter
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }
    }

    // Bool to Visibility Converter
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue;
            return false;
        }
    }

    // Invert Bool to Visibility Converter
    public class InvertBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }
    }

    // Int to String Converter (for age display)
    public class IntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && intValue > 0)
                return intValue.ToString();
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (int.TryParse(value as string, out int result))
                return result;
            return 0;
        }
    }

    // String Truncate Converter
    public class StringTruncateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string input = value as string;
            if (string.IsNullOrEmpty(input))
                return input;

            int maxLength = 50;
            if (parameter != null && int.TryParse(parameter.ToString(), out int paramLength))
                maxLength = paramLength;

            return input.Length <= maxLength ? input : input.Substring(0, maxLength - 3) + "...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Join List to String Converter (for interests)
    public class JoinListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.IEnumerable list)
            {
                var items = new List<string>();
                foreach (var item in list)
                {
                    items.Add(item?.ToString() ?? "");
                }
                return string.Join(", ", items);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // DateTime to Relative Time Converter
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var difference = DateTime.Now - dateTime;

                if (difference.TotalMinutes < 1)
                    return "Just now";
                if (difference.TotalMinutes < 60)
                    return $"{(int)difference.TotalMinutes}m ago";
                if (difference.TotalHours < 24)
                    return $"{(int)difference.TotalHours}h ago";
                if (difference.TotalDays < 7)
                    return $"{(int)difference.TotalDays}d ago";
                if (difference.TotalDays < 30)
                    return $"{(int)(difference.TotalDays / 7)}w ago";
                if (difference.TotalDays < 365)
                    return dateTime.ToString("MMM d");

                return dateTime.ToString("MMM d, yyyy");
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}