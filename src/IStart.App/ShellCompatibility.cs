using System.Windows;

namespace IStart;

internal static class ShellCompatibility
{
    internal static bool CanTry(Version version) => version.Major == 10 && version.Minor == 0
        && (version.Build is >= 19041 and <= 19045 or 22000 or 22621 or 22631 or 26100 or 26200 or 26300);

    internal static string Description(Version version) => !CanTry(version)
        ? L.T("此版本不在试用范围内，保持 Windows 原生菜单。")
        : version.Build == 26200 ? L.T("此版本已验证基本接入；启用前仍会检测开始按钮。")
        : L.T("此版本允许兼容试用，尚未实机验收；定位成功后才接入，可随时切回原生菜单。");

    internal static bool ValidBounds(Rect button, Rect bar)
    {
        if (button.IsEmpty || bar.IsEmpty || !double.IsFinite(button.X) || !double.IsFinite(button.Y)
            || !double.IsFinite(button.Width) || !double.IsFinite(button.Height)) return false;
        var limit = Math.Max(128, Math.Min(bar.Width, bar.Height) * 3);
        return button.Width >= 4 && button.Height >= 4 && button.Width <= limit && button.Height <= limit && bar.Contains(button);
    }
}
