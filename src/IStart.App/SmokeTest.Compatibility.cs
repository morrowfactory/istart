using System.Runtime.InteropServices;
using System.Windows;

namespace IStart;

internal static partial class SmokeTest
{
    private delegate nint WindowProcedure(nint window, uint message, nint first, nint second);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size, Style;
        public WindowProcedure Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? Menu, Name;
        public nint SmallIcon;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint extended, string name, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint window, uint message, nint first, nint second);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);

    private static void VerifyCompatibility()
    {
        foreach (var build in new[] { 19041, 19042, 19043, 19044, 19045, 22000, 22621, 22631, 26100, 26200 })
            Check(ShellCompatibility.CanTry(new Version(10, 0, build)), "已知 Win10/11 版本允许能力检测试用");
        foreach (var version in new[] { new Version(6, 3, 9600), new Version(10, 0, 18363), new Version(10, 0, 20348), new Version(10, 0, 99999) })
            Check(!ShellCompatibility.CanTry(version), "旧系统、服务器和未知版本保留原生入口");
        var bar = new Rect(-1920, 0, 1920, 48);
        Check(ShellCompatibility.ValidBounds(new(-1910, 0, 48, 48), bar), "负坐标显示器按钮范围有效");
        Check(!ShellCompatibility.ValidBounds(new(-1920, 0, 1920, 48), bar)
            && !ShellCompatibility.ValidBounds(new(0, 0, 48, 48), bar)
            && !ShellCompatibility.ValidBounds(Rect.Empty, bar), "拒绝整条任务栏、越界与空按钮");

        // 只在本进程创建屏幕外、不激活的夹具窗口；不修改 Explorer 或注入输入。
        var module = GetModuleHandle(null);
        WindowProcedure procedure = DefWindowProc;
        var registered = new List<string>();
        nint root = 0;
        try
        {
            foreach (var name in new[] { "IStart.CompatibilityFixture", "Start" })
            {
                var windowClass = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Instance = module, Name = name, Procedure = procedure };
                Check(RegisterClassEx(ref windowClass) != 0, "创建本进程原生按钮类夹具");
                registered.Add(name);
            }
            root = CreateWindowEx(0x08000080, registered[0], "", 0x90000000, -20000, -20000, 300, 48, 0, 0, module, 0);
            Check(root != 0, "创建屏幕外任务栏夹具");
            Check(CreateWindowEx(0, "Start", "", 0x50000000, 0, 0, 48, 48, root, 0, module, 0) != 0, "创建可识别传统开始按钮");
            Check(CreateWindowEx(0, "Start", "", 0x40000000, 50, 0, 48, 48, root, 0, module, 0) != 0, "创建隐藏按钮夹具");
            Check(CreateWindowEx(0, "Start", "", 0x50000000, 400, 0, 48, 48, root, 0, module, 0) != 0, "创建越界按钮夹具");
            WinInput.GetWindowRect(root, out var rect);
            var found = new List<StartTarget>();
            StartMenuAdapter.FindNativeStart(root, new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), found);
            Check(found.Count == 1 && found[0].Source == "NativeStart", "原生 Start 子窗口定位，过滤隐藏和越界窗口");
        }
        finally
        {
            if (root != 0) DestroyWindow(root);
            foreach (var name in registered.AsEnumerable().Reverse()) UnregisterClass(name, module);
            GC.KeepAlive(procedure);
        }
    }
}
