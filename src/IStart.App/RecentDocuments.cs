using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal static class RecentDocuments
{
    internal static List<LaunchRecord> Read(CancellationToken token, string? folder = null)
    {
        var includeApplicationHistory = folder is null;
        folder ??= Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        if (!Directory.Exists(folder)) return [];
        var found = new Dictionary<string, LaunchRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in new DirectoryInfo(folder).EnumerateFiles("*.lnk")
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
        if (!includeApplicationHistory) return found.Values.Take(100).ToList();
        var applicationHistory = JumpListDocuments.Read(folder, token);
        try { applicationHistory.AddRange(LibreOfficeDocuments.Read(token)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException) { }
        try { applicationHistory.AddRange(WpsDocuments.Read(token)); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        return Merge(found.Values, applicationHistory).ToList();
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
