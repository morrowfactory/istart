using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace IStart;

internal static class JumpListDocuments
{
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int StgOpenStorage(string path, nint priority, uint mode, nint exclude, uint reserved, out IStorage storage);
    [ComImport, Guid("0000000B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStorage
    {
        void CreateStream();
        void OpenStream([MarshalAs(UnmanagedType.LPWStr)] string name, nint reserved, uint mode, uint reserved2, out IStream stream);
        void CreateStorage();
        void OpenStorage();
        void CopyTo();
        void MoveElementTo();
        void Commit();
        void Revert();
        void EnumElements(uint reserved, nint reserved2, uint reserved3, out IEnumStat enumerator);
    }
    [ComImport, Guid("0000000D-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumStat
    {
        [PreserveSig] int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] STATSTG[] values, out uint fetched);
    }
    internal static List<LaunchRecord> Read(string recentFolder, CancellationToken token)
    {
        var folder = Path.Combine(recentFolder, "AutomaticDestinations");
        if (!Directory.Exists(folder)) return [];
        var result = new List<LaunchRecord>();
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*.automaticDestinations-ms")
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(60))
        {
            token.ThrowIfCancellationRequested();
            if (file.Length > 16 * 1024 * 1024 || StgOpenStorage(file.FullName, 0, 0x20, 0, 0, out var storage) < 0) continue;
            IEnumStat? enumerator = null;
            try
            {
                storage.EnumElements(0, 0, 0, out enumerator);
                var values = new STATSTG[1];
                for (var i = 0; i < 200 && enumerator.Next(1, values, out var fetched) == 0 && fetched == 1; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var stat = values[0];
                    if (stat.type != 2 || stat.pwcsName == "DestList" || stat.cbSize is < 76 or > 1048576) continue;
                    IStream? stream = null;
                    try
                    {
                        storage.OpenStream(stat.pwcsName, 0, 0x10, 0, out stream);
                        var target = ShellShortcut.ReadTarget(stream);
                        if (!RecentDocuments.IsDocument(target)) continue;
                        // Stream/container timestamps are approximate; do not claim exact access times.
                        var time = ((long)stat.mtime.dwHighDateTime << 32) | (uint)stat.mtime.dwLowDateTime;
                        var opened = time > 0 ? DateTimeOffset.FromFileTime(time) : new DateTimeOffset(file.LastWriteTimeUtc);
                        result.Add(new(target, Path.GetFileName(target), opened, 1));
                    }
                    catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
                    finally { if (stream is not null) Marshal.FinalReleaseComObject(stream); }
                }
            }
            catch (COMException) { }
            finally
            {
                if (enumerator is not null) Marshal.FinalReleaseComObject(enumerator);
                Marshal.FinalReleaseComObject(storage);
            }
        }
        return result;
    }
}
