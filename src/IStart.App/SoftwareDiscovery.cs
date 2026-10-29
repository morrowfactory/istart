using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IStart;

internal sealed record SoftwareCandidate(string Title, string Shortcut, string? Executable = null, string? AppId = null);

internal static class SoftwareDiscovery
{
    public static List<SoftwareCandidate> Read(CancellationToken token, IEnumerable<string>? roots = null,
        IReadOnlyDictionary<string, int>? usage = null)
    {
        var includePackagedApps = roots is null;
        var found = new Dictionary<string, SoftwareCandidate>(StringComparer.OrdinalIgnoreCase);
        roots ??= new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var shortcut in Directory.EnumerateFiles(root, "*.lnk", options))
            {
                token.ThrowIfCancellationRequested();
                var title = Path.GetFileNameWithoutExtension(shortcut);
                if (title.Contains("uninstall", StringComparison.OrdinalIgnoreCase) || title.Contains("setup", StringComparison.OrdinalIgnoreCase)
                    || title.Contains("卸载") || title.Contains("安装")) continue;
                try
                {
                    var target = ShellShortcut.ReadTarget(shortcut);
                    if (!string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) continue;
                    // Keep the original shortcut so application arguments and working directory survive.
                    found.TryAdd(Path.GetFullPath(target), new(title, shortcut, target, ReadAppId(shortcut)));
                }
                catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
            }
        }
        if (includePackagedApps) ReadPackagedApps(found, token);
        if (usage is not null) ReadUsedExecutables(found, usage, token);
        return found.Values.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private static void ReadUsedExecutables(Dictionary<string, SoftwareCandidate> found,
        IReadOnlyDictionary<string, int> usage, CancellationToken token)
    {
        // Portable apps may have activity evidence without any Start-menu shortcut.
        var added = 0;
        foreach (var item in usage.Where(p => p.Value > 0).OrderByDescending(p => p.Value).Take(2048))
        {
            token.ThrowIfCancellationRequested();
            var path = item.Key;
            if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal)
                || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                path = Path.GetFullPath(path);
                if (found.ContainsKey(path) || new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (new[] { "setup", "install", "unins", "update", "crash", "卸载", "安装" }
                    .Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    || new[] { "rundll32", "dllhost", "conhost", "msiexec", "werfault", "applicationframehost" }
                        .Contains(name, StringComparer.OrdinalIgnoreCase) || !File.Exists(path)) continue;
                var version = FileVersionInfo.GetVersionInfo(path);
                var title = !string.IsNullOrWhiteSpace(version.FileDescription) ? version.FileDescription.Trim()
                    : !string.IsNullOrWhiteSpace(version.ProductName) ? version.ProductName.Trim() : name;
                if (found.TryAdd(path, new(title, path, path)) && ++added == 128) break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                or System.Security.SecurityException) { }
        }
    }
    private static string? ReadAppId(string shortcut)
    {
        object? shell = null, folder = null, item = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace(Path.GetDirectoryName(shortcut));
            if (folder is null) return null;
            item = ((dynamic)folder).ParseName(Path.GetFileName(shortcut));
            return item is null ? null : ((dynamic)item).ExtendedProperty("System.AppUserModel.ID") as string;
        }
        catch (COMException) { return null; }
        finally
        {
            if (item is not null) Marshal.FinalReleaseComObject(item);
            if (folder is not null) Marshal.FinalReleaseComObject(folder);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
    private static void ReadPackagedApps(Dictionary<string, SoftwareCandidate> found, CancellationToken token)
    {
        object? shell = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
            if (folder is null) return;
            items = ((dynamic)folder).Items();
            var count = (int)((dynamic)items).Count;
            for (var i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                object item = ((dynamic)items).Item(i);
                try
                {
                    string id = ((dynamic)item).Path;
                    string title = ((dynamic)item).Name;
                    // Packaged application IDs have a package-family/application separator.
                    if (id.Contains('!') && !string.IsNullOrWhiteSpace(title))
                        found.TryAdd(id, new(title, "shell:AppsFolder\\" + id));
                }
                finally { Marshal.FinalReleaseComObject(item); }
            }
        }
        catch (COMException) { }
        finally
        {
            if (items is not null) Marshal.FinalReleaseComObject(items);
            if (folder is not null) Marshal.FinalReleaseComObject(folder);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
