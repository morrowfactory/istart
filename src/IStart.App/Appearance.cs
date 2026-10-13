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
        Brush("Surface", windows ? "#F3F6FB" : "#F8FAFD");
        Brush("ContentSurface", "#FFFFFF");
        Brush("TextPrimary", windows ? "#1B1F24" : "#202124");
        Brush("TextSecondary", windows ? "#616A77" : "#5F6368");
        Brush("TextMuted", windows ? "#616A77" : "#80868B");
        Brush("Accent", windows ? "#0067C0" : "#0B57D0");
        Brush("AccentSoft", windows ? "#EAF2FA" : "#E8F0FE");
        Brush("ButtonSurface", windows ? "#FFFFFF" : "#E8F0FE");
        Brush("ButtonOutline", windows ? "#DDE3EB" : "#00000000");
        Brush("Outline", windows ? "#DDE3EB" : "#DADCE0");
        Brush("CardOutline", windows ? "#E3E8EF" : "#00000000");
        Brush("HoverSurface", windows ? "#F0F4F9" : "#F1F5FC");
        Brush("ButtonText", windows ? "#1B1F24" : "#0B57D0");
        Brush("SelectionText", windows ? "#1B1F24" : "#0B57D0");
        Brush("InputFocusOutline", windows ? "#DDE3EB" : "#0B57D0");
        Brush("StatusSurface", windows ? "#EBF0F6" : "#E8F0FE");
        Brush("SettingsSurface", windows ? "#FFFFFF" : "#00000000");
        resources["ButtonRadius"] = new CornerRadius(windows ? 6 : 16);
        resources["InputRadius"] = new CornerRadius(windows ? 6 : 14);
        resources["ItemRadius"] = new CornerRadius(windows ? 8 : 12);
        resources["CardRadius"] = new CornerRadius(windows ? 12 : 20);
        resources["StatusRadius"] = new CornerRadius(windows ? 8 : 12);
        resources["BrandRadius"] = new CornerRadius(windows ? 6 : 10);
        resources["WindowPadding"] = new Thickness(windows ? 20 : 16);
        resources["ContentPadding"] = new Thickness(windows ? 16 : 14);
        resources["ButtonPadding"] = new Thickness(12, windows ? 6 : 7, 12, windows ? 6 : 7);
        resources["SearchPadding"] = new Thickness(36, windows ? 10 : 12, 12, windows ? 10 : 12);
        resources["SearchBorder"] = new Thickness(windows ? 1 : 0);
        resources["TileWidth"] = windows ? 64.0 : 78.0;
        resources["TileHeight"] = windows ? 62.0 : 68.0;
        resources["TileFontSize"] = windows ? 12.0 : 13.0;
        resources["BrandIconSize"] = windows ? 20.0 : 18.0;
        resources["BrandTitleSize"] = windows ? 21.0 : 23.0;
        resources["StatusPadding"] = new Thickness(windows ? 6 : 10);
        resources["StatusFontSize"] = windows ? 11.0 : 13.0;
        resources["AccountMinWidth"] = windows ? 98.0 : 0.0;
        resources["MenuFont"] = new FontFamily(windows ? "Segoe UI Variable, Microsoft YaHei UI" : "Microsoft YaHei UI");
        resources["SelectionIndicatorVisibility"] = windows ? Visibility.Visible : Visibility.Collapsed;
        resources["WindowsBrandVisibility"] = windows ? Visibility.Visible : Visibility.Collapsed;
        resources["GoogleBrandVisibility"] = windows ? Visibility.Collapsed : Visibility.Visible;
    }
}
