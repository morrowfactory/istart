using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

public sealed class ShellIconConverter : IValueConverter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
        public nint Icon; public int IconIndex; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Display;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path) return null;
        if (Cache.TryGetValue(path, out var icon)) return icon;
        var flags = path.StartsWith("\\\\", StringComparison.Ordinal) ? 0x111u : 0x101u;
        if (SHGetFileInfo(path, 0x80, out var info, (uint)Marshal.SizeOf<FileInfo>(), flags) == 0 || info.Icon == 0) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            if (Cache.Count < 256) Cache.TryAdd(path, source);
            return source;
        }
        finally { DestroyIcon(info.Icon); }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
