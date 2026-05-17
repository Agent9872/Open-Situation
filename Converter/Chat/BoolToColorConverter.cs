using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Lock.Converter.Chat;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool isSelected)
            return Colors.Transparent;

        // parameter tells us what we're coloring: border, text, background, icon, button, etc.
        string? param = parameter?.ToString()?.ToLowerInvariant();

        return param switch
        {
            // Tab background
            "tab" or "tabbackground" =>
                isSelected ? Color.FromArgb("#2A2A2E") : Colors.Transparent,

            // Icon color
            "icon" or "iconcolor" =>
                isSelected ? Color.FromArgb("#C05050") : Color.FromArgb("#888888"),

            // Text color
            "text" or "textcolor" =>
                isSelected ? Color.FromArgb("#C05050") : Color.FromArgb("#888888"),

            // Button color (for message buttons - registered vs unregistered)
            "button" or "buttoncolor" =>
                isSelected ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FFA500"),

            // Background for message buttons
            "buttonbg" or "buttonbackground" =>
                isSelected ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FFA500"),

            // Stroke / Border
            "active" or "stroke" or "border" =>
                isSelected ? Color.FromArgb("#C05050") : Colors.Transparent,

            // Switch track color
            "switchtrack" =>
                isSelected ? Color.FromArgb("#C05050") : Color.FromArgb("#4A4A4A"),

            // Switch thumb color
            "switchthumb" =>
                isSelected ? Colors.White : Color.FromArgb("#E0E0E0"),

            // Badge background
            "badge" or "badgebackground" =>
                isSelected ? Color.FromArgb("#C05050") : Color.FromArgb("#4A4A4A"),

            // Fallback / default behavior
            _ => isSelected ? Colors.White : Color.FromArgb("#888888")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}