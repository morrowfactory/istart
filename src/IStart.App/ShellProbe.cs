using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace IStart;

internal static class ShellProbe
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string? title);

    public static string Read()
    {
        var handle = FindWindow("Shell_TrayWnd", null);
        if (handle == 0) return "未发现交互式任务栏；接入未验证。";
        try
        {
            var tray = AutomationElement.FromHandle(handle);
            var buttons = tray.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"));
            return $"系统版本：{Environment.OSVersion.Version}\n任务栏：已发现\nAutomationId=StartButton 数量：{buttons.Count}\n只读发现，不代表接管或恢复验证通过。";
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
        { return "任务栏存在，但自动化发现不可用；未接管。"; }
    }
}
