using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal static class RecentDocuments
{
    internal static List<LaunchRecord> Read(CancellationToken token, string? folder = null, Action? failed = null)
    {
        var includeApplicationHistory = folder is null;
        folder ??= Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        var sources = new List<Func<List<LaunchRecord>>> { () => ReadLinks(folder, token), () => JumpListDocuments.Read(folder, token) };
        if (includeApplicationHistory)
            sources.AddRange([() => LibreOfficeDocuments.Read(token), () => WpsDocuments.Read(token), () => FreeCadDocuments.Read(token)]);
        return Merge([], ReadSources(token, sources, failed)).ToList();
    }
    internal static List<LaunchRecord> ReadSources(CancellationToken token, IEnumerable<Func<List<LaunchRecord>>> sources, Action? failed = null)
    {
        var records = new List<LaunchRecord>();
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            try { records.AddRange(source()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException
                or System.Xml.XmlException or System.Security.SecurityException or ArgumentException)
            { failed?.Invoke(); }
        }
        return records;
    }
    private static List<LaunchRecord> ReadLinks(string folder, CancellationToken token)
    {
        var found = new Dictionary<string, LaunchRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in (Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles("*.lnk") : [])
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(200))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var target = ShellShortcut.ReadTarget(link.FullName);
                // Do not probe network shares or execute/resolve links while listing history.
                if (!IsDocument(target)) continue;
                found.TryAdd(target, new(target, Path.GetFileName(target), new DateTimeOffset(link.LastWriteTimeUtc), 1));
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return found.Values.ToList();
    }
    internal static bool IsDocument(string target) => !string.IsNullOrWhiteSpace(target)
        && !target.StartsWith("\\\\", StringComparison.Ordinal) && Path.IsPathFullyQualified(target)
        && File.Exists(target) && !new[] { ".exe", ".lnk", ".dll", ".bat", ".cmd", ".ps1" }.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase);
    internal static IEnumerable<LaunchRecord> Merge(IEnumerable<LaunchRecord> own, IEnumerable<LaunchRecord> windows)
        => own.Concat(windows).OrderByDescending(r => r.OpenedAt)
            .DistinctBy(r => r.Target, StringComparer.OrdinalIgnoreCase).Take(100);
    internal static IEnumerable<LaunchRecord> Files(IEnumerable<LaunchRecord> own, IEnumerable<LaunchRecord> windows)
        => Merge(own.Where(r => IsDocument(r.Target)), windows.Where(r => IsDocument(r.Target)));
}
