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
        Brush("ContentSurface", windows ? "#F3F3F3" : "#FFFFFF");
        Brush("TextPrimary", windows ? "#1B1F24" : "#202124");
        Brush("TextSecondary", windows ? "#616A77" : "#5F6368");
        Brush("TextMuted", windows ? "#616A77" : "#80868B");
        Brush("Accent", windows ? "#0067C0" : "#0B57D0");
        Brush("AccentSoft", windows ? "#E6EEF5" : "#E8F0FE");
        Brush("ButtonSurface", windows ? "#FFFFFF" : "#E8F0FE");
        Brush("ButtonOutline", windows ? "#DDE3EB" : "#00000000");
        Brush("Outline", windows ? "#DDE3EB" : "#DADCE0");
        Brush("HoverSurface", windows ? "#F0F4F9" : "#F1F5FC");
        Brush("ButtonText", windows ? "#1B1F24" : "#0B57D0");
        Brush("SelectionText", windows ? "#1B1F24" : "#0B57D0");
        Brush("InputFocusOutline", windows ? "#DDE3EB" : "#0B57D0");
        Brush("StatusSurface", windows ? "#EBF0F6" : "#E8F0FE");
        Brush("SearchSurface", windows ? "#FFFFFF" : "#E8F0FE");
        Brush("FooterSurface", windows ? "#E9E9E9" : "#F8FAFD");
        Brush("NavigationSelection", windows ? "#00000000" : "#E8F0FE");
        Brush("AddActionSurface", windows ? "#0067C0" : "#E8F0FE");
        Brush("AddActionText", windows ? "#FFFFFF" : "#0B57D0");
        Brush("AccountSurface", windows ? "#FFFFFF" : "#E8F0FE");
        Brush("AccountText", windows ? "#616161" : "#0B57D0");
        resources["ButtonRadius"] = new CornerRadius(windows ? 4 : 20);
        resources["ActionRadius"] = new CornerRadius(windows ? 4 : 20);
        resources["NavigationRadius"] = new CornerRadius(windows ? 4 : 20);
        resources["InputRadius"] = new CornerRadius(windows ? 4 : 22);
        resources["ItemRadius"] = new CornerRadius(windows ? 4 : 12);
        resources["CardRadius"] = new CornerRadius(windows ? 0 : 24);
        resources["WindowPadding"] = windows ? new Thickness(0) : new Thickness(20, 0, 20, 20);
        resources["HeaderMargin"] = windows ? new Thickness(24, 0, 24, 0) : new Thickness(0);
        resources["ContentMargin"] = windows ? new Thickness(24, 0, 24, 0) : new Thickness(0);
        resources["NavigationMargin"] = windows ? new Thickness(24, 0, 24, 0) : new Thickness(0, 4, 12, 0);
        resources["StatusMargin"] = windows ? new Thickness(24, 4, 24, 0) : new Thickness(0, 4, 0, 0);
        resources["FooterPadding"] = windows ? new Thickness(24, 0, 24, 0) : new Thickness(0);
        resources["ContentPadding"] = windows ? new Thickness(0, 12, 0, 16) : new Thickness(16);
        resources["HeaderHeight"] = new GridLength(windows ? 56 : 72);
        resources["SearchHeight"] = windows ? 34.0 : 40.0;
        resources["ActionSize"] = windows ? 36.0 : 40.0;
        resources["HeaderActionSize"] = windows ? 32.0 : 40.0;
        resources["AppColumns"] = windows ? 6 : 5;
        resources["NavigationAlignment"] = windows ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        resources["ButtonPadding"] = new Thickness(12, windows ? 6 : 7, 12, windows ? 6 : 7);
        resources["SearchBorder"] = new Thickness(windows ? 1 : 0);
        resources["TileHeight"] = windows ? 80.0 : 84.0;
        resources["MenuFont"] = new FontFamily(windows ? "Segoe UI Variable, Microsoft YaHei UI" : "Segoe UI, Microsoft YaHei UI");
        resources["SelectionIndicatorVisibility"] = windows ? Visibility.Visible : Visibility.Collapsed;
        resources["WindowsBrandVisibility"] = windows ? Visibility.Visible : Visibility.Collapsed;
        resources["GoogleBrandVisibility"] = windows ? Visibility.Collapsed : Visibility.Visible;
    }
}
