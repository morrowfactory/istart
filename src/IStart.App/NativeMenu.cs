using System.ComponentModel;

namespace IStart;

internal static class NativeMenu
{
    public static void Open()
    {
        if (!WinInput.Keys((0x11, false), (0x1B, false), (0x1B, true), (0x11, true))) throw new Win32Exception();
    }
}
