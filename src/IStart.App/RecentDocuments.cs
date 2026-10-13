using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal static class RecentDocuments
{
    internal static List<LaunchRecord> Read(CancellationToken token, string? folder = null)
    {
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
                if (string.IsNullOrWhiteSpace(target) || target.StartsWith("\\\\", StringComparison.Ordinal)
                    || !Path.IsPathFullyQualified(target) || !File.Exists(target)
                    || Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                found.TryAdd(target, new(target, Path.GetFileName(target), new DateTimeOffset(link.LastWriteTimeUtc), 1));
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return found.Values.Take(100).ToList();
    }
    internal static IEnumerable<LaunchRecord> Merge(IEnumerable<LaunchRecord> own, IEnumerable<LaunchRecord> windows)
        => own.Concat(windows).OrderByDescending(r => r.OpenedAt)
            .DistinctBy(r => r.Target, StringComparer.OrdinalIgnoreCase).Take(100);
}
