using System.IO;
using Microsoft.Win32;

namespace IStart;

internal static class MySoftware
{
    internal static Dictionary<string, int> ReadUsage(CancellationToken token)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppSwitched");
            if (key is null) return result;
            foreach (var name in key.GetValueNames().Take(2048))
            {
                token.ThrowIfCancellationRequested();
                if (key.GetValue(name) is not int count || count <= 0) continue;
                var identity = name.Replace("{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase)
                    .Replace("{6D809377-6AF0-444B-8957-A3773F02200E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);
                result[identity] = Math.Max(count, result.GetValueOrDefault(identity));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        return result;
    }

    internal static List<MenuEntry> Build(IEnumerable<MenuEntry> pinned, IEnumerable<SoftwareCandidate> candidates,
        IEnumerable<LaunchRecord> launches, IReadOnlyDictionary<string, int> usage, ISet<string> hidden, string query = "")
    {
        var apps = candidates.ToList();
        var groups = new EntryGroups();
        var records = launches.ToDictionary(r => r.Target, StringComparer.OrdinalIgnoreCase);
        var choices = new Dictionary<string, (SoftwareCandidate App, MenuEntry Entry, bool Pinned)>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps)
            choices.TryAdd(app.Executable ?? app.Shortcut, (app, new(Guid.NewGuid(), app.Shortcut, app.Title, Guid.Empty), false));
        foreach (var entry in pinned.Where(groups.IsApplication).Where(e => e.Target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal) || File.Exists(e.Target)))
        {
            var app = apps.FirstOrDefault(c => c.Shortcut.Equals(entry.Target, StringComparison.OrdinalIgnoreCase)
                || c.Executable?.Equals(entry.Target, StringComparison.OrdinalIgnoreCase) == true) ?? new(entry.Title, entry.Target, entry.Target);
            choices[app.Executable ?? app.Shortcut] = (app, entry, true);
        }
        var ranked = choices.Values.Select(item =>
        {
            var app = item.App;
            var identities = new[] { app.Shortcut, app.Executable, app.AppId, app.Shortcut.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal) ? app.Shortcut[17..] : null };
            var system = identities.Where(id => id is not null).Select(id => usage.GetValueOrDefault(id!) ).DefaultIfEmpty().Max();
            var own = identities.Where(id => id is not null).Select(id => records.GetValueOrDefault(id!)).Where(r => r is not null).ToList();
            return (item.Entry, item.Pinned, Count: Math.Max(system, own.Select(r => r!.Count).DefaultIfEmpty().Max()),
                Latest: own.Select(r => r!.OpenedAt).DefaultIfEmpty().Max());
        });
        return ranked.Where(item => !hidden.Contains(item.Entry.Target)
                && (query.Length > 0 || item.Pinned || item.Count > 0)
                && (item.Entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Entry.Target.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.Count).ThenByDescending(item => item.Latest)
            .ThenBy(item => item.Entry.Title, StringComparer.CurrentCultureIgnoreCase).Take(30).Select(item => item.Entry).ToList();
    }
}
