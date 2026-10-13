using System.IO;

namespace IStart;

internal sealed class EntryGroups
{
    private readonly Dictionary<string, bool> applications = new(StringComparer.OrdinalIgnoreCase);
    internal bool IsApplication(MenuEntry entry)
    {
        if (applications.TryGetValue(entry.Target, out var cached)) return cached;
        var target = entry.Target;
        if (Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && !target.StartsWith("\\\\", StringComparison.Ordinal))
            try { target = ShellShortcut.ReadTarget(target); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
        var application = target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal)
            || new[] { ".exe", ".appref-ms", ".bat", ".cmd", ".ps1" }.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase);
        if (applications.Count < 256) applications[entry.Target] = application;
        return application;
    }
}
