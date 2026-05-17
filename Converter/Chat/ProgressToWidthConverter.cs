using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class ProgressToWidthConverter : IValueConverter
    {
        // Shared static width that gets updated when layout changes
        public static double WaveformColumnWidth { get; set; } = 160.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                double progress = 0;
                if (value is double d) progress = d;
                else if (value != null)
                    progress = System.Convert.ToDouble(value);

                progress = Math.Clamp(progress, 0, 1);

                // Use parameter if explicitly provided
                if (parameter is string s && double.TryParse(s,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double p) && p > 0)
                    return progress * p;

                // Use the shared measured width (updated by SizeChanged in ChatPage)
                double width = WaveformColumnWidth > 0 ? WaveformColumnWidth : 160.0;
                return progress * width;
            }
            catch { return 0.0; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}