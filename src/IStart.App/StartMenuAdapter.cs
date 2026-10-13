using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;

namespace IStart;

internal sealed record StartTarget(nint Taskbar, Rect Bounds);

internal sealed class WinKeyGesture
{
    private readonly HashSet<uint> pressed = [];
    private bool combined;
    public bool Update(uint key, bool down, bool modifiers)
    {
        if (key is not (0x5B or 0x5C)) { if (down && pressed.Count > 0) combined = true; return false; }
        if (down)
        {
            if (pressed.Count == 0) combined = modifiers;
            else if (!pressed.Contains(key)) combined = true;
            pressed.Add(key);
            return false;
        }
        if (!pressed.Remove(key)) return false;
        return pressed.Count == 0 && !combined && !modifiers;
    }
    public void Reset() { pressed.Clear(); combined = false; }
}

internal sealed class StartMenuAdapter : IDisposable
{
    private delegate nint Hook(int code, nint message, nint data);
    private delegate bool EnumWindow(nint window, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct KeyData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public WinInput.Point Point; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, Hook callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int max);
    private readonly Dispatcher dispatcher;
    private readonly Func<StartTarget[]> scanTargets;
    private readonly Action<WinInput.Point?> toggle;
    private readonly Hook keyboardCallback, mouseCallback;
    private readonly WinKeyGesture gesture = new();
    private readonly DispatcherTimer timer;
    private nint keyboard, mouse;
    private StartTarget[] targets = [];
    private long updated;
    private bool scanning, suppressMouseUp, disposed;
    private bool waitingForTaskbar;
    private Task<StartTarget[]>? pendingScan;
    public bool Enabled => keyboard != 0 && mouse != 0;
    public event Action<string>? StatusChanged;
    internal IReadOnlyList<StartTarget> Targets => targets;
    public StartMenuAdapter(Dispatcher dispatcher, Action<WinInput.Point?> toggle, Func<StartTarget[]>? scanTargets = null)
    {
        this.dispatcher = dispatcher;
        this.toggle = toggle;
        this.scanTargets = scanTargets ?? FindTargets;
        keyboardCallback = KeyboardHook;
        mouseCallback = MouseHook;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, async (_, _) => await RefreshTargets(), dispatcher);
        timer.Stop();
    }
    public async Task Enable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Enabled) return;
        if (Environment.OSVersion.Version.Build != 26200) throw new NotSupportedException("此系统版本尚未完成入口验证，请先使用原生菜单模式。");
        await RefreshTargets();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (targets.Length == 0) throw new NotSupportedException("未能定位开始按钮，已保留 Windows 原生入口。");
        try
        {
            keyboard = SetWindowsHookEx(13, keyboardCallback, GetModuleHandle(null), 0);
            mouse = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
            if (keyboard == 0 || mouse == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            timer.Start();
        }
        catch { Disable(); throw; }
    }
    internal async Task RefreshTargets()
    {
        if (scanning || disposed) return;
        scanning = true;
        try
        {
            pendingScan ??= Task.Run(scanTargets);
            var snapshot = await pendingScan.WaitAsync(TimeSpan.FromSeconds(2));
            pendingScan = null;
            if (disposed) return;
            if (snapshot.Length > 0)
            {
                targets = snapshot;
                updated = Environment.TickCount64;
                SetWaitingForTaskbar(false);
            }
            else SetWaitingForTaskbar(true);
        }
        catch (Exception ex) when (ex is TimeoutException or COMException or ElementNotAvailableException or InvalidOperationException)
        {
            if (pendingScan?.IsCompleted == true) pendingScan = null;
            SetWaitingForTaskbar(true);
        }
        finally { scanning = false; }
    }
    private void SetWaitingForTaskbar(bool waiting)
    {
        // 保留上次成功快照，但鼠标钩子仍严格执行 1.5 秒新鲜度与窗口校验。
        // 短暂 UIAutomation 空结果不能撤销用户偏好或停用独立的键盘接入。
        if (waitingForTaskbar == waiting) return;
        waitingForTaskbar = waiting;
        StatusChanged?.Invoke(waiting
            ? "开始按钮检测暂时不可用，正在重试；Win 键接入保留。"
            : "开始按钮检测已恢复，自定义菜单继续可用。");
    }
    internal static StartTarget[] FindTargets()
    {
        var bars = new List<nint>();
        EnumWindows((window, _) =>
        {
            var name = new System.Text.StringBuilder(128);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") bars.Add(window);
            return true;
        }, 0);
        var result = new List<StartTarget>();
        foreach (var bar in bars)
        {
            if (!WinInput.IsWindowVisible(bar)) continue;
            var root = AutomationElement.FromHandle(bar);
            var found = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"));
            foreach (AutomationElement element in found)
            {
                var rect = element.Current.BoundingRectangle;
                if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0 && !element.Current.IsOffscreen) result.Add(new(bar, rect));
            }
        }
        return result.ToArray();
    }
    private nint KeyboardHook(int code, nint message, nint data)
    {
        if (code >= 0 && Enabled)
        {
            var key = Marshal.PtrToStructure<KeyData>(data);
            if (key.Extra != WinInput.Marker)
            {
                var down = message is 0x100 or 0x104;
                if (gesture.Update(key.Key, down, WinInput.ModifiersHeld) && WinInput.CancelWinRelease((ushort)key.Key))
                {
                    dispatcher.BeginInvoke(() => { if (Enabled) toggle(null); });
                    return 1;
                }
            }
        }
        return CallNextHookEx(0, code, message, data);
    }
    private nint MouseHook(int code, nint message, nint data)
    {
        if (code >= 0 && Enabled && message is 0x201 or 0x202)
        {
            var point = Marshal.PtrToStructure<MouseData>(data).Point;
            if (message == 0x202 && suppressMouseUp)
            {
                suppressMouseUp = false;
                dispatcher.BeginInvoke(() => { if (Enabled) toggle(point); });
                return 1;
            }
            if (message == 0x201 && !WinInput.ModifiersHeld && Environment.TickCount64 - updated < 1500)
            {
                var root = WinInput.GetAncestor(WinInput.WindowFromPoint(point), 2);
                if (targets.Any(t => t.Taskbar == root && t.Bounds.Contains(point.X, point.Y)
                    && WinInput.IsWindowVisible(t.Taskbar) && WinInput.GetWindowRect(t.Taskbar, out var bounds)
                    && point.X >= bounds.Left && point.X < bounds.Right && point.Y >= bounds.Top && point.Y < bounds.Bottom))
                { suppressMouseUp = true; return 1; }
            }
        }
        return CallNextHookEx(0, code, message, data);
    }
    public void Disable()
    {
        timer.Stop();
        if (keyboard != 0) UnhookWindowsHookEx(keyboard);
        if (mouse != 0) UnhookWindowsHookEx(mouse);
        keyboard = mouse = 0;
        suppressMouseUp = false;
        gesture.Reset();
        targets = [];
        waitingForTaskbar = false;
    }
    public void Dispose() { if (disposed) return; disposed = true; Disable(); }
}
