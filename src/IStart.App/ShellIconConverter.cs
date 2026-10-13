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
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")] private static extern nint SHGetFileInfo(nint item, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint context, out nint item, uint attributes, out uint found);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string path, int index, out nint large, out nint small, uint count);
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<BitmapImage> WebsiteIcon = new(() =>
    {
        var icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Fluent/website.png"));
        icon.Freeze();
        return icon;
    });
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path) return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return WebsiteIcon.Value;
        var cacheKey = path;
        if (Cache.TryGetValue(path, out var icon)) return icon;
        if (path.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal))
        {
            nint item = 0;
            try
            {
                if (SHParseDisplayName(path, 0, out item, 0, out _) >= 0 && item != 0
                    && SHGetFileInfo(item, 0, out var appInfo, (uint)Marshal.SizeOf<FileInfo>(), 0x108) != 0 && appInfo.Icon != 0)
                {
                    try
                    {
                        var bitmap = Imaging.CreateBitmapSourceFromHIcon(appInfo.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bitmap.Freeze();
                        if (Cache.Count < 256) Cache.TryAdd(cacheKey, bitmap);
                        return bitmap;
                    }
                    finally { DestroyIcon(appInfo.Icon); }
                }
            }
            finally { if (item != 0) Marshal.FreeCoTaskMem(item); }
            // A failed shell lookup must still display a recognizable generic application icon.
            path = ".exe";
        }
        if (!path.StartsWith("\\\\", StringComparison.Ordinal) && Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var location = ShellShortcut.ReadIconLocation(path);
                if (!location.Path.StartsWith("\\\\", StringComparison.Ordinal) && File.Exists(location.Path))
                {
                    ExtractIconEx(location.Path, location.Index, out var large, out var small, 1);
                    try
                    {
                        var selected = large != 0 ? large : small;
                        if (selected != 0)
                        {
                            var bitmap = Imaging.CreateBitmapSourceFromHIcon(selected, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            bitmap.Freeze();
                            if (Cache.Count < 256) Cache.TryAdd(cacheKey, bitmap);
                            return bitmap;
                        }
                    }
                    finally { if (large != 0) DestroyIcon(large); if (small != 0 && small != large) DestroyIcon(small); }
                }
                // 显示目标图标，启动仍使用原快捷方式，保留参数与工作目录。
                path = ShellShortcut.ReadTarget(path);
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        }
        var flags = path == ".exe" || path.StartsWith("\\\\", StringComparison.Ordinal) ? 0x111u : 0x101u;
        if (SHGetFileInfo(path, 0x80, out var info, (uint)Marshal.SizeOf<FileInfo>(), flags) == 0 || info.Icon == 0) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            if (Cache.Count < 256) Cache.TryAdd(cacheKey, source);
            return source;
        }
        finally { DestroyIcon(info.Icon); }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
