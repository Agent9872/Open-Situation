using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class AmplitudeToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // ── Parse max height from parameter ──
                double maxHeight = 36.0;
                if (parameter is string paramStr && double.TryParse(paramStr,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    maxHeight = parsed;
                else if (parameter is double pd) maxHeight = pd;
                else if (parameter is int pi) maxHeight = pi;

                // ── Extract amplitude value ──
                double amplitude = 50.0;
                if (value is int i) amplitude = i;
                else if (value is double d) amplitude = d;
                else if (value is float f) amplitude = f;
                else if (value is byte b) amplitude = b;
                else if (value is string s && double.TryParse(s,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double sp))
                    amplitude = sp;
                else if (value != null)
                {
                    try { amplitude = System.Convert.ToDouble(value); }
                    catch { amplitude = 50.0; }
                }

                // ── Clamp to 0-100 ──
                amplitude = Math.Clamp(amplitude, 0, 100);

                // ── Apply a curve to exaggerate differences ──
                // Low amplitudes stay low, high amplitudes reach near max
                // This gives the WhatsApp-style varied look
                double normalized = amplitude / 100.0;

                // Apply power curve: makes quiet parts clearly shorter
                // and loud parts clearly taller
                double curved = Math.Pow(normalized, 0.65);

                // Minimum bar height = 4px (always visible)
                // Maximum bar height = maxHeight
                double minHeight = 4.0;
                double height = minHeight + (curved * (maxHeight - minHeight));

                // Add a tiny random-like variation based on amplitude value
                // so adjacent bars of similar amplitude still look slightly different
                // Uses amplitude itself as a deterministic seed (no actual Random)
                double micro = (amplitude % 7) * 0.3;
                height += micro;

                // Final clamp to ensure we stay within bounds
                height = Math.Clamp(height, minHeight, maxHeight);

                return height;
            }
            catch
            {
                return 8.0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}