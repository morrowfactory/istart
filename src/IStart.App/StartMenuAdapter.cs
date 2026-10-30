using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;

namespace IStart;

internal sealed record StartTarget(nint Taskbar, Rect Bounds, string Source = "UIAutomation", nint ButtonWindow = 0);

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
    private delegate bool EnumWindow(nint window, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct KeyData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public WinInput.Point Point; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindow callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int max);
    private readonly Dispatcher dispatcher;
    private readonly Func<StartTarget[]> scanTargets;
    private readonly bool nativeFallback;
    private readonly Version systemVersion;
    private readonly Action<WinInput.Point?> toggle;
    private readonly InputHookThread.Callback keyboardCallback, mouseCallback;
    private readonly WinKeyGesture gesture = new();
    private readonly DispatcherTimer timer;
    private volatile InputHookThread? hookThread;
    private volatile StartTarget[] targets = [];
    private long updated, uiHeartbeat = Environment.TickCount64;
    private int generation, snapshotGeneration, queuedToggle;
    private Task? enableTask;
    private bool scanning, suppressMouseUp, disposed;
    private bool waitingForTaskbar;
    private Task<StartTarget[]>? pendingScan;
    public bool Enabled => hookThread?.Running == true;
    public event Action<string>? StatusChanged;
    internal IReadOnlyList<StartTarget> Targets => targets;
    public StartMenuAdapter(Dispatcher dispatcher, Action<WinInput.Point?> toggle, Func<StartTarget[]>? scanTargets = null, Version? systemVersion = null)
    {
        this.dispatcher = dispatcher;
        this.toggle = toggle;
        this.scanTargets = scanTargets ?? FindTargets;
        nativeFallback = scanTargets is null;
        this.systemVersion = systemVersion ?? Environment.OSVersion.Version;
        keyboardCallback = KeyboardHook;
        mouseCallback = MouseHook;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, async (_, _) => await RefreshTargets(), dispatcher);
        timer.Stop();
    }
    public Task Enable() => enableTask is { IsCompleted: false } ? enableTask : enableTask = EnableCore();
    private async Task EnableCore()
    {
        var requestedGeneration = generation;
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Enabled) return;
        if (!ShellCompatibility.CanTry(systemVersion)) throw new NotSupportedException(ShellCompatibility.Description(systemVersion));
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await RefreshTargets();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (requestedGeneration != generation) throw new NotSupportedException(L.T("开始入口启用已取消，保留原生菜单。"));
            if (targets.Length > 0) break;
            if (attempt < 3) await Task.Delay(400);
        }
        ObjectDisposedException.ThrowIf(disposed, this);
        if (requestedGeneration != generation) throw new NotSupportedException(L.T("开始入口启用已取消，保留原生菜单。"));
        if (targets.Length == 0) throw new NotSupportedException(L.T("未能定位开始按钮，已保留 Windows 原生入口。"));
        try
        {
            var started = new InputHookThread(keyboardCallback, mouseCallback);
            hookThread = started;
            await started.Ready;
            if (disposed || requestedGeneration != generation) { started.Dispose(); throw new NotSupportedException(L.T("开始入口启用已取消，保留原生菜单。")); }
            uiHeartbeat = Environment.TickCount64;
            RuntimeLog.Write("input-hooks dedicated-thread=True");
            timer.Start();
        }
        catch { Disable(); throw; }
    }
    internal async Task RefreshTargets()
    {
        Volatile.Write(ref uiHeartbeat, Environment.TickCount64);
        if (scanning || disposed) return;
        scanning = true;
        var scanGeneration = snapshotGeneration;
        try
        {
            pendingScan ??= Task.Run(scanTargets);
            var snapshot = await pendingScan.WaitAsync(TimeSpan.FromSeconds(2));
            pendingScan = null;
            if (disposed || scanGeneration != snapshotGeneration) return;
            if (snapshot.Length > 0)
            {
                if (!targets.SequenceEqual(snapshot)) RuntimeLog.Write($"start-targets count={snapshot.Length} sources={string.Join(',', snapshot.Select(t => t.Source).Distinct())}");
                targets = snapshot;
                updated = Environment.TickCount64;
                SetWaitingForTaskbar(false);
            }
            else SetWaitingForTaskbar(true);
        }
        catch (Exception ex) when (ex is TimeoutException or COMException or ElementNotAvailableException or InvalidOperationException)
        {
            if (pendingScan?.IsCompleted == true) pendingScan = null;
            if (disposed || scanGeneration != snapshotGeneration) return;
            var native = nativeFallback ? ReadTargets(true) : [];
            if (native.Length > 0)
            {
                targets = native;
                Volatile.Write(ref updated, Environment.TickCount64);
                SetWaitingForTaskbar(false);
                return;
            }
            SetWaitingForTaskbar(true);
        }
        finally { scanning = false; }
    }
    private void SetWaitingForTaskbar(bool waiting)
    {
        // UIAutomation 使用 1.5 秒有效期；原生按钮在点击时实时校验 HWND 和边界。
        // 短暂 UIAutomation 空结果不能撤销用户偏好或停用独立的键盘接入。
        if (waitingForTaskbar == waiting) return;
        waitingForTaskbar = waiting;
        StatusChanged?.Invoke(waiting
            ? L.T("开始按钮检测暂时不可用，正在重试；Win 键接入保留。")
            : L.T("开始按钮检测已恢复，自定义菜单继续可用。"));
    }
    internal static StartTarget[] FindTargets() => ReadTargets(false);
    private static StartTarget[] ReadTargets(bool nativeOnly)
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
            if (!WinInput.IsWindowVisible(bar) || !WinInput.GetWindowRect(bar, out var windowRect)) continue;
            var barBounds = new Rect(windowRect.Left, windowRect.Top, Math.Max(0, windowRect.Right - windowRect.Left), Math.Max(0, windowRect.Bottom - windowRect.Top));
            var before = result.Count;
            // Win10 传统任务栏可直接提供 Start 子窗口，无需依赖自动化标识或显示语言。
            if (nativeOnly || Environment.OSVersion.Version.Build <= 19045) FindNativeStart(bar, barBounds, result);
            if (nativeOnly || result.Count > before) continue;
            try
            {
                var root = AutomationElement.FromHandle(bar);
                var found = root.FindAll(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
                foreach (AutomationElement element in found)
                {
                    var rect = element.Current.BoundingRectangle;
                    if (!element.Current.IsOffscreen && ShellCompatibility.ValidBounds(rect, barBounds)) result.Add(new(bar, rect));
                }
            }
            catch (Exception ex) when (ex is COMException or ElementNotAvailableException or InvalidOperationException) { }
            if (result.Count == before) FindNativeStart(bar, barBounds, result);
        }
        return result.Distinct().ToArray();
    }
    internal static void FindNativeStart(nint bar, Rect bounds, List<StartTarget> result)
    {
        EnumChildWindows(bar, (window, _) =>
        {
            var name = new System.Text.StringBuilder(128);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() == "Start" && WinInput.GetAncestor(window, 2) == bar
                && WinInput.IsWindowVisible(window) && WinInput.GetWindowRect(window, out var rect))
            {
                var button = new Rect(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));
                if (ShellCompatibility.ValidBounds(button, bounds)) result.Add(new(bar, button, "NativeStart", window));
            }
            return true;
        }, 0);
    }
    private nint KeyboardHook(int code, nint message, nint data)
    {
        if (code >= 0 && Enabled && !UiResponsive) gesture.Reset();
        if (code >= 0 && Enabled && UiResponsive)
        {
            var key = Marshal.PtrToStructure<KeyData>(data);
            if (key.Extra != WinInput.Marker)
            {
                var down = message is 0x100 or 0x104;
                if (gesture.Update(key.Key, down, WinInput.ModifiersHeld) && WinInput.CancelWinRelease((ushort)key.Key))
                {
                    QueueToggle(null);
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
                QueueToggle(point);
                return 1;
            }
            if (message == 0x201 && !WinInput.ModifiersHeld && UiResponsive)
            {
                var root = WinInput.GetAncestor(WinInput.WindowFromPoint(point), 2);
                if (targets.Any(t => t.Taskbar == root && HitTarget(t, point)
                    && WinInput.IsWindowVisible(t.Taskbar) && WinInput.GetWindowRect(t.Taskbar, out var bounds)
                    && point.X >= bounds.Left && point.X < bounds.Right && point.Y >= bounds.Top && point.Y < bounds.Bottom))
                { suppressMouseUp = true; return 1; }
            }
        }
        return CallNextHookEx(0, code, message, data);
    }
    private bool UiResponsive => Environment.TickCount64 - Volatile.Read(ref uiHeartbeat) < 2000;
    private void QueueToggle(WinInput.Point? point)
    {
        if (Interlocked.Exchange(ref queuedToggle, 1) != 0) return;
        var queuedAt = Environment.TickCount64;
        var requestedGeneration = Volatile.Read(ref generation);
        dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref queuedToggle, 0);
            var delay = Environment.TickCount64 - queuedAt;
            if (delay > 100) RuntimeLog.Write($"menu-dispatch-delay elapsedMs={delay}");
            if (Enabled && requestedGeneration == generation) toggle(point);
        });
    }
    internal bool HitTarget(StartTarget target, WinInput.Point point)
    {
        if (target.ButtonWindow == 0) return Environment.TickCount64 - Volatile.Read(ref updated) < 1500 && target.Bounds.Contains(point.X, point.Y);
        // Win10 原生 Start HWND 在点击时重新读取，检测慢或任务栏移动不会令旧坐标生效。
        var name = new System.Text.StringBuilder(128);
        GetClassName(target.ButtonWindow, name, name.Capacity);
        if (name.ToString() != "Start" || WinInput.GetAncestor(target.ButtonWindow, 2) != target.Taskbar
            || !WinInput.IsWindowVisible(target.ButtonWindow) || !WinInput.GetWindowRect(target.ButtonWindow, out var rect)
            || !WinInput.GetWindowRect(target.Taskbar, out var bar)) return false;
        var current = new Rect(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));
        return ShellCompatibility.ValidBounds(current, new Rect(bar.Left, bar.Top, Math.Max(0, bar.Right - bar.Left), Math.Max(0, bar.Bottom - bar.Top)))
            && current.Contains(point.X, point.Y);
    }
    internal void ShellChanged()
    {
        snapshotGeneration++;
        targets = [];
        Volatile.Write(ref updated, 0);
        if (Enabled) _ = RefreshTargets();
        RuntimeLog.Write("taskbar-layout-invalidated");
    }
    public void Disable()
    {
        timer.Stop();
        Interlocked.Increment(ref generation);
        snapshotGeneration++;
        var previous = hookThread;
        hookThread = null;
        previous?.Dispose();
        suppressMouseUp = false;
        gesture.Reset();
        targets = [];
        waitingForTaskbar = false;
    }
    public void Dispose() { if (disposed) return; disposed = true; Disable(); }
}
