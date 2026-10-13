using System.IO;
using Microsoft.Win32;

namespace IStart;

internal static class WpsDocuments
{
    internal static List<LaunchRecord> Read(CancellationToken token)
    {
        var result = new List<LaunchRecord>();
        foreach (var app in new[] { "wps", "et", "wpp", "pdf", "ofd" })
        {
            using var files = Registry.CurrentUser.OpenSubKey($"Software\\Kingsoft\\Office\\6.0\\{app}\\RecentFiles\\files", false);
            if (files is null) continue;
            foreach (var name in files.GetSubKeyNames().OrderBy(n => int.TryParse(n, out var index) ? index : int.MaxValue).Take(100))
            {
                token.ThrowIfCancellationRequested();
                using var entry = files.OpenSubKey(name, false);
                if (entry?.GetValue("path") is not string target || !RecentDocuments.IsDocument(target)) continue;
                // This WPS schema has ordering but no confirmed open timestamp.
                result.Add(new(target, Path.GetFileName(target), DateTimeOffset.MinValue, 1));
            }
        }
        return result;
    }
}
