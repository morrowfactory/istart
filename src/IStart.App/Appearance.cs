using System.Windows;
using System.Windows.Media;

namespace IStart;

internal static class Appearance
{
    internal static void Apply(string theme)
    {
        var windows = theme == "Windows";
        var resources = Application.Current.Resources;
        void Brush(string key, string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            resources[key] = brush;
        }
        Brush("Surface", windows ? "#F3F3F3" : "#F8FAFD");
        Brush("ContentSurface", windows ? "#FAFAFA" : "#FFFFFF");
        Brush("TextPrimary", windows ? "#1B1B1B" : "#202124");
        Brush("TextSecondary", windows ? "#505050" : "#5F6368");
        Brush("TextMuted", windows ? "#666666" : "#80868B");
        Brush("Accent", windows ? "#0067C0" : "#0B57D0");
        Brush("AccentSoft", windows ? "#E5F1FB" : "#E8F0FE");
        Brush("ButtonSurface", windows ? "#FFFFFF" : "#E8F0FE");
        Brush("ButtonOutline", windows ? "#D1D1D1" : "#00000000");
        Brush("Outline", windows ? "#C8C8C8" : "#DADCE0");
        Brush("CardOutline", windows ? "#E0E0E0" : "#00000000");
        Brush("HoverSurface", windows ? "#E9E9E9" : "#F1F5FC");
        resources["ButtonRadius"] = new CornerRadius(windows ? 4 : 16);
        resources["InputRadius"] = new CornerRadius(windows ? 4 : 14);
        resources["ItemRadius"] = new CornerRadius(windows ? 4 : 12);
        resources["CardRadius"] = new CornerRadius(windows ? 8 : 20);
        resources["StatusRadius"] = new CornerRadius(windows ? 4 : 12);
        resources["BrandRadius"] = new CornerRadius(windows ? 4 : 10);
        resources["SelectionIndicatorVisibility"] = windows ? Visibility.Visible : Visibility.Collapsed;
        resources["WindowsBrandVisibility"] = windows ? Visibility.Visible : Visibility.Collapsed;
        resources["GoogleBrandVisibility"] = windows ? Visibility.Collapsed : Visibility.Visible;
    }
}
