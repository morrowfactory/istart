using System.Windows;

namespace IStart;

internal static class MenuPlacement
{
    internal static Rect Calculate(Rect work, Rect button, Size menu)
    {
        var width = Math.Min(menu.Width, work.Width);
        var height = Math.Min(menu.Height, work.Height);
        var gapX = Math.Min(8, (work.Width - width) / 2);
        var gapY = Math.Min(8, (work.Height - height) / 2);
        var x = Math.Clamp(button.Left, work.Left + gapX, work.Right - width - gapX);
        var y = work.Bottom - height - gapY;
        if (button.Bottom <= work.Top) y = work.Top + gapY;
        else if (button.Right <= work.Left || button.Left >= work.Right)
        {
            x = button.Right <= work.Left ? work.Left + gapX : work.Right - width - gapX;
            y = Math.Clamp(button.Top, work.Top + gapY, work.Bottom - height - gapY);
        }
        return new Rect(x, y, width, height);
    }
}
