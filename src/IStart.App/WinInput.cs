using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IStart;

internal static class WinInput
{
    internal const nuint Marker = 0x49535441;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct Union { [FieldOffset(0)] public Mouse Mouse; [FieldOffset(0)] public Keyboard Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public Union Value; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern nint GetFocus();
    [DllImport("user32.dll")] internal static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    private static Input Key(ushort key, bool up, nuint marker) => new() { Type = 1, Value = new() { Keyboard = new() { Key = key, Flags = up ? 2u : 0u, Extra = marker } } };
    internal static bool Keys(params (ushort Key, bool Up)[] keys) => Send(keys.Select(k => Key(k.Key, k.Up, Marker)).ToArray());
    internal static bool TestKeys(params (ushort Key, bool Up)[] keys) => Send(keys.Select(k => Key(k.Key, k.Up, 0)).ToArray());
    private static bool Send(Input[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    internal static bool CancelWinRelease(ushort key) => Keys((0x11, false), (0x11, true), (key, true));
    internal static bool TryForegroundAfterMenuInput(nint window)
    {
        if (GetForegroundWindow() == window) return true;
        if (ModifiersHeld || (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) return false;
        // 仅响应用户打开菜单后的失败焦点请求；带标记且平衡释放，Ctrl 取消 Alt 菜单激活。
        if (!Keys((0x12, false), (0x11, false), (0x11, true), (0x12, true))) return false;
        SetForegroundWindow(window);
        return GetForegroundWindow() == window;
    }
    internal static bool ModifiersHeld => new[] { 0x10, 0x11, 0x12 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    internal static void TestClick(Point point)
    {
        GetCursorPos(out var original);
        try
        {
            SetCursorPos(point.X, point.Y);
            if (!Send([new() { Value = new() { Mouse = new() { Flags = 2 } } }, new() { Value = new() { Mouse = new() { Flags = 4 } } }])) throw new Win32Exception();
        }
        finally { SetCursorPos(original.X, original.Y); }
    }
    internal static string ForegroundProcess()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName; }
        catch (ArgumentException) { return "unknown"; }
    }
}
