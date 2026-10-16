using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace IStart;

internal sealed class ScreenshotHotkey : IDisposable
{
    internal static readonly string[] Choices = ["Alt+Shift+A", "Ctrl+Alt+A", "Ctrl+Shift+S", "关闭"];
    private const int Id = 0x4953;
    private readonly HwndSource source;
    private readonly Action capture;
    private bool registered;
    private string shortcut = "关闭";
    internal string Status => Binding(shortcut).Key == 0 ? L.T("截屏快捷键已关闭。")
        : registered ? L.F("截屏快捷键：{0}", shortcut)
        : L.F("{0} 无法注册，可能已被企业微信等软件占用；请选择其他快捷键。", shortcut);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);

    internal ScreenshotHotkey(HwndSource source, Action capture)
    {
        this.source = source;
        this.capture = capture;
        source.AddHook(HandleMessage);
    }
    internal static (uint Modifiers, uint Key) Binding(string? shortcut) => shortcut switch
    {
        "Alt+Shift+A" => (0x0001 | 0x0004, 0x41),
        "Ctrl+Alt+A" => (0x0002 | 0x0001, 0x41),
        "Ctrl+Shift+S" => (0x0002 | 0x0004, 0x53),
        _ => (0, 0)
    };
    internal void Set(string shortcut)
    {
        this.shortcut = shortcut;
        if (registered) UnregisterHotKey(source.Handle, Id);
        registered = false;
        var (modifiers, key) = Binding(shortcut);
        if (key == 0) return;
        registered = RegisterHotKey(source.Handle, Id, modifiers | 0x4000, key);
        RuntimeLog.Write("screenshot-hotkey registered=" + registered);
    }
    private nint HandleMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == Id && registered) { handled = true; capture(); }
        return 0;
    }
    public void Dispose()
    {
        if (registered) UnregisterHotKey(source.Handle, Id);
        registered = false;
        source.RemoveHook(HandleMessage);
    }
}
