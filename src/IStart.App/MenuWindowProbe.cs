using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IStart;

internal static class MenuWindowProbe
{
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(nint window, out uint color, out byte alpha, out uint flags);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo
    {
        public uint Size, Flags;
        public nint Active, Focus, Capture, Menu, MoveSize, Caret;
        public WinInput.Rect CaretRect;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);

    internal static List<object> Read()
    {
        using var current = Process.GetCurrentProcess();
        var pids = new HashSet<uint>();
        foreach (var process in Process.GetProcessesByName("IStart.App"))
        {
            using (process)
                if (process.SessionId == current.SessionId) pids.Add((uint)process.Id);
        }
        var result = new List<object>();
        EnumWindows((window, unused) =>
        {
            var thread = WinInput.GetWindowThreadProcessId(window, out var pid);
            if (!pids.Contains(pid)) return true;
            WinInput.GetWindowRect(window, out var bounds);
            var cloaked = DwmGetWindowAttribute(window, 14, out var value, sizeof(int)) == 0 ? value : -1;
            var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
            GetGUIThreadInfo(thread, ref info);
            var layered = GetLayeredWindowAttributes(window, out _, out var alpha, out _);
            result.Add(new { Pid = pid, Thread = thread, Window = window.ToInt64(), Owner = GetWindow(window, 4).ToInt64(),
                Visible = WinInput.IsWindowVisible(window), Enabled = IsWindowEnabled(window), Minimized = IsIconic(window), Cloaked = cloaked,
                TitleLength = GetWindowTextLength(window), Style = GetWindowLongPtr(window, -16).ToInt64().ToString("X"),
                ExStyle = GetWindowLongPtr(window, -20).ToInt64().ToString("X"), Layered = layered, Alpha = alpha,
                GuiFlags = info.Flags, Active = info.Active.ToInt64(), Focus = info.Focus.ToInt64(), Capture = info.Capture.ToInt64(),
                Left = bounds.Left, Top = bounds.Top, Width = bounds.Right - bounds.Left, Height = bounds.Bottom - bounds.Top });
            return true;
        }, 0);
        return result;
    }
}
