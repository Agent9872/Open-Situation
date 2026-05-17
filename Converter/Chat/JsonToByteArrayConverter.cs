using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Controls;

namespace Lock.Converter.Chat
{
    public class JsonToByteArrayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string json && !string.IsNullOrEmpty(json))
            {
                try
                {
                    // Try to deserialize as int array first (for waveform amplitudes)
                    var intArray = JsonSerializer.Deserialize<int[]>(json);
                    if (intArray != null)
                        return intArray;

                    // Try as byte array
                    var byteArray = JsonSerializer.Deserialize<byte[]>(json);
                    if (byteArray != null)
                        return byteArray;

                    // Try as list of int
                    var intList = JsonSerializer.Deserialize<List<int>>(json);
                    if (intList != null)
                        return intList.ToArray();
                }
                catch
                {
                    // Return empty array on error
                    return new int[0];
                }
            }

            // Return default waveform if no data
            return GenerateDefaultWaveform();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<int> enumerable)
            {
                return JsonSerializer.Serialize(enumerable);
            }
            throw new NotImplementedException();
        }

        private int[] GenerateDefaultWaveform()
        {
            // Generate a default waveform pattern for voice messages without data
            var random = new Random();
            var waveform = new int[20];
            for (int i = 0; i < 20; i++)
            {
                // Create a bell curve-ish pattern
                double position = (double)i / 19; // 0 to 1
                double amplitude = Math.Sin(position * Math.PI) * 0.5 + 0.5; // 0.5 to 1.0
                waveform[i] = (int)(20 + (amplitude * 50) + (random.NextDouble() * 10 - 5));
                waveform[i] = Math.Clamp(waveform[i], 10, 100);
            }
            return waveform;
        }
    }
}