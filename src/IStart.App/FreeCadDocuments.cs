using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace IStart;

internal static class FreeCadDocuments
{
    internal static List<LaunchRecord> Read(CancellationToken token, string? root = null)
    {
        token.ThrowIfCancellationRequested();
        root ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FreeCAD");
        if (!Directory.Exists(root)) return [];
        var configs = new List<string> { Path.Combine(root, "user.cfg") };
        try
        {
            configs.AddRange(new DirectoryInfo(root).EnumerateDirectories("v*", new EnumerationOptions
                { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                .OrderByDescending(d => d.LastWriteTimeUtc).Take(8).Select(d => Path.Combine(d.FullName, "user.cfg")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var result = new List<LaunchRecord>();
        foreach (var path in configs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) continue;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                    { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
                var xml = XDocument.Load(reader);
                var group = xml.Root;
                foreach (var name in new[] { "Root", "BaseApp", "Preferences", "RecentFiles" })
                    group = group?.Elements("FCParamGroup").FirstOrDefault(e => (string?)e.Attribute("Name") == name);
                if (group is null) continue;
                // MRU0 is newest. FreeCAD stores no per-file opening timestamp here.
                var recent = group.Elements("FCText").Select(e => (Value: e.Value, Key: (string?)e.Attribute("Name")))
                    .Select(e => (e.Value, Index: e.Key?.StartsWith("MRU", StringComparison.Ordinal) == true
                        && int.TryParse(e.Key.AsSpan(3), out var index) ? index : -1))
                    .Where(e => e.Index is >= 0 and < 100).OrderBy(e => e.Index).Take(100);
                var timestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(path));
                foreach (var item in recent)
                {
                    token.ThrowIfCancellationRequested();
                    var target = item.Value;
                    if (!Path.IsPathFullyQualified(target) || target.StartsWith("\\\\", StringComparison.Ordinal)
                        || new DriveInfo(Path.GetPathRoot(target)!).DriveType == DriveType.Network
                        || !RecentDocuments.IsDocument(target)) continue;
                    result.Add(new(target, Path.GetFileName(target), timestamp, 1));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException
                or ArgumentException or System.Security.SecurityException) { }
        }
        return RecentDocuments.Merge([], result).ToList();
    }
}
