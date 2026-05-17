using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lock.Converter.Post
{
    public class HasImagesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IList<string> imagePaths && imagePaths != null)
            {
                return imagePaths.Any(p => !string.IsNullOrEmpty(p));
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FirstImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle string[] (from your Post model)
            if (value is string[] stringArray && stringArray.Length > 0 && !string.IsNullOrEmpty(stringArray[0]))
            {
                try
                {
                    return ImageSource.FromFile(stringArray[0]);
                }
                catch
                {
                    return null;
                }
            }

            // Handle IList<string> (from other sources)
            if (value is IList<string> imagePaths && imagePaths != null && imagePaths.Any())
            {
                var firstImage = imagePaths.FirstOrDefault(p => !string.IsNullOrEmpty(p));
                if (!string.IsNullOrEmpty(firstImage) && System.IO.File.Exists(firstImage))
                {
                    return ImageSource.FromFile(firstImage);
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}