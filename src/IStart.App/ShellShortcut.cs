using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace IStart;

internal static class ShellShortcut
{
    private static object NewLink() => Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
    public static string ReadTarget(string path)
    {
        var link = NewLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var buffer = new StringBuilder(32768);
            ((IShellLinkW)link).GetPath(buffer, buffer.Capacity, 0, 4);
            return Environment.ExpandEnvironmentVariables(buffer.ToString());
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }
    internal static void CreateFixture(string path, string target)
    {
        var link = NewLink();
        try { ((IShellLinkW)link).SetPath(target); ((IPersistFile)link).Save(path, true); }
        finally { Marshal.FinalReleaseComObject(link); }
    }
    internal static (string Path, int Index) ReadIconLocation(string path)
    {
        var link = NewLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var buffer = new StringBuilder(32768);
            ((IShellLinkW)link).GetIconLocation(buffer, buffer.Capacity, out var index);
            if (buffer.Length == 0) ((IShellLinkW)link).GetPath(buffer, buffer.Capacity, 0, 4);
            return (Environment.ExpandEnvironmentVariables(buffer.ToString()), index);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, nint findData, uint flags);
        void GetIDList(out nint list);
        void SetIDList(nint list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int max);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int max);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int max);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
