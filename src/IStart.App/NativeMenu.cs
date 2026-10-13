using System.Runtime.InteropServices;

namespace IStart;

internal static class NativeMenu
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, nuint extraInfo);

    // This prototype does not install hooks; Ctrl+Esc is handed directly to Windows.
    public static void Open()
    {
        keybd_event(0x11, 0, 0, 0);
        keybd_event(0x1B, 0, 0, 0);
        keybd_event(0x1B, 0, 2, 0);
        keybd_event(0x11, 0, 2, 0);
    }
}
