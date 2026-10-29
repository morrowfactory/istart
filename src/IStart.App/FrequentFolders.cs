using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal static class FrequentFolders
{
    internal static bool IsLocalFolder(string target) => !string.IsNullOrWhiteSpace(target)
        && !target.StartsWith("\\\\", StringComparison.Ordinal) && Path.IsPathFullyQualified(target)
        && Directory.Exists(target);

    internal static List<LaunchRecord> Read(CancellationToken token, string? recentFolder = null)
    {
        recentFolder ??= Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        if (!Directory.Exists(recentFolder)) return [];
        var records = new List<LaunchRecord>();
        foreach (var link in new DirectoryInfo(recentFolder).EnumerateFiles("*.lnk")
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(200))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var target = ShellShortcut.ReadTarget(link.FullName);
                if (IsLocalFolder(target)) records.Add(new(target, Path.GetFileName(Path.TrimEndingDirectorySeparator(target)), new DateTimeOffset(link.LastWriteTimeUtc), 1));
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return RecentDocuments.Merge(records, JumpListDocuments.Read(recentFolder, token, folders: true)).ToList();
    }

    internal static List<MenuEntry> Build(IEnumerable<MenuEntry> pinned, IEnumerable<LaunchRecord> own,
        IEnumerable<LaunchRecord> external, int capacity, Guid category, ISet<string> hidden, bool recent = false, string query = "")
    {
        var result = pinned.Where(e => IsLocalFolder(e.Target))
            .DistinctBy(e => Path.TrimEndingDirectorySeparator(e.Target), StringComparer.OrdinalIgnoreCase).ToList();
        var known = result.ToDictionary(e => Path.TrimEndingDirectorySeparator(e.Target), StringComparer.OrdinalIgnoreCase);
        var ownRecords = own.ToList();
        var ownCounts = ownRecords.ToDictionary(r => r.Target, r => r.Count, StringComparer.OrdinalIgnoreCase);
        var ranked = new Dictionary<string, (long Count, DateTimeOffset Latest)>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in ownRecords.Concat(external).OrderByDescending(r => r.OpenedAt)
            .DistinctBy(r => r.Target, StringComparer.OrdinalIgnoreCase).Take(300))
        {
            var folder = IsLocalFolder(record.Target) ? Path.TrimEndingDirectorySeparator(record.Target)
                : RecentDocuments.IsDocument(record.Target) ? Path.GetDirectoryName(record.Target) : null;
            if (recent && !IsLocalFolder(record.Target)) continue;
            if (folder is null || !IsLocalFolder(folder) || hidden.Contains(folder)) continue;
            var previous = ranked.GetValueOrDefault(folder);
            ranked[folder] = (previous.Count + Math.Max(record.Count, ownCounts.GetValueOrDefault(record.Target)), previous.Latest > record.OpenedAt ? previous.Latest : record.OpenedAt);
        }
        // Counts represent iStart launches or distinct recent-file activity, not system-wide visits.
        if (!recent) foreach (var entry in known.Keys.Where(path => !hidden.Contains(path))) ranked.TryAdd(entry, (0, DateTimeOffset.MinValue));
        result.Clear();
        var sorted = recent ? ranked.OrderByDescending(p => p.Value.Latest).ThenByDescending(p => p.Value.Count)
            : ranked.OrderByDescending(p => p.Value.Count).ThenByDescending(p => p.Value.Latest);
        foreach (var item in sorted.ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Where(p => p.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || known.GetValueOrDefault(p.Key)?.Title.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .Take(Math.Max(capacity, known.Count)))
        {
            var title = Path.GetFileName(item.Key.TrimEnd('\\'));
            result.Add(known.GetValueOrDefault(item.Key) ?? new(Guid.NewGuid(), item.Key, title.Length == 0 ? item.Key : title, category));
        }
        return result;
    }
}
