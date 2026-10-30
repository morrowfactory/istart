using System.Windows;

namespace IStart;

internal static class MenuPlacement
{
    internal static Size OpeningSize(Size current, Size minimum, DpiScale dpi) => new(
        Math.Max(double.IsFinite(current.Width) ? current.Width : minimum.Width, minimum.Width) * dpi.DpiScaleX,
        Math.Max(double.IsFinite(current.Height) ? current.Height : minimum.Height, minimum.Height) * dpi.DpiScaleY);
    internal static Rect Calculate(Rect work, Size menu)
    {
        var width = Math.Min(menu.Width, work.Width);
        var height = Math.Min(menu.Height, work.Height);
        var gapX = Math.Min(8, (work.Width - width) / 2);
        var gapY = Math.Min(8, (work.Height - height) / 2);
        return new Rect(work.Left + gapX, work.Bottom - height - gapY, width, height);
    }
}
