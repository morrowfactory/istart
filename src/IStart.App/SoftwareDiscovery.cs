using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IStart;

internal sealed record SoftwareCandidate(string Title, string Shortcut, string? Executable = null, string? AppId = null, IReadOnlyList<string>? RegisteredIds = null)
{
    internal string Identity => (AppId ?? (RegisteredIds is { Count: 1 } ? RegisteredIds[0] : null)) is { Length: > 0 } id
        ? "app-id:" + id : Executable ?? Shortcut;
}

internal static class SoftwareDiscovery
{
    public static List<SoftwareCandidate> Read(CancellationToken token, IEnumerable<string>? roots = null,
        IReadOnlyDictionary<string, int>? usage = null)
    {
        var useSystemSources = roots is null;
        var found = new Dictionary<string, SoftwareCandidate>(StringComparer.OrdinalIgnoreCase);
        roots ??= new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) };
        foreach (var root in roots) ReadShortcuts(found, root, true, token);
        if (useSystemSources)
        {
            ReadShortcuts(found, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false, token);
            ReadShortcuts(found, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), false, token);
            ReadPackagedApps(found, token);
            ReadRegisteredExecutables(found, token);
        }
        if (usage is not null) ReadUsedExecutables(found, usage, token);
        return found.Values.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private static void ReadShortcuts(Dictionary<string, SoftwareCandidate> found, string root, bool recursive, CancellationToken token)
    {
        try
        {
            if (!Path.IsPathFullyQualified(root) || root.StartsWith(@"\\", StringComparison.Ordinal)
                || new DriveInfo(Path.GetPathRoot(root)!).DriveType == DriveType.Network || !Directory.Exists(root)) return;
            var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            var shortcuts = Directory.EnumerateFiles(root, "*.lnk", options);
            foreach (var shortcut in recursive ? shortcuts : shortcuts.Take(128))
            {
                token.ThrowIfCancellationRequested();
                var title = Path.GetFileNameWithoutExtension(shortcut);
                if (title.Contains("uninstall", StringComparison.OrdinalIgnoreCase) || title.Contains("setup", StringComparison.OrdinalIgnoreCase)
                    || title.Contains("卸载") || title.Contains("安装")) continue;
                try
                {
                    var target = ShellShortcut.ReadTarget(shortcut);
                    if (!IsLocalExecutable(target)) continue;
                    // Keep the original shortcut so application arguments and working directory survive.
                    var app = new SoftwareCandidate(title, shortcut, Path.GetFullPath(target), ReadAppId(shortcut));
                    found.TryAdd(app.Identity, app);
                }
                catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException) { }
    }

    private static void ReadRegisteredExecutables(Dictionary<string, SoftwareCandidate> found, CancellationToken token)
    {
        const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps") + Path.DirectorySeparatorChar;
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(appPaths);
                if (key is null) continue;
                foreach (var name in key.GetSubKeyNames().Take(512))
                {
                    token.ThrowIfCancellationRequested();
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    using var entry = key.OpenSubKey(name);
                    if (entry?.GetValue(null) is not string value) continue;
                    var path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                    if (!name.Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase) || IsMaintenanceExecutable(path)
                        || !IsLocalExecutable(path)) continue;
                    paths.Add(Path.GetFullPath(path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException) { }
        }
        var existing = found.Values.Where(c => c.Executable is not null).ToLookup(c => c.Executable!, StringComparer.OrdinalIgnoreCase);
        var ids = ReadRegisteredIds(paths, token);
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            var registeredIds = ids[path].ToArray();
            if (!existing.Contains(path)) found.TryAdd(path, new(ExecutableTitle(path), path, path, RegisteredIds: registeredIds));
            else foreach (var app in existing[path]) found[app.Identity] = app with { RegisteredIds = registeredIds };
        }
    }

    private static ILookup<string, string> ReadRegisteredIds(HashSet<string> paths, CancellationToken token)
    {
        var targets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var classes = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var registered = root.OpenSubKey(@"SOFTWARE\RegisteredApplications");
                    if (registered is null) continue;
                    foreach (var name in registered.GetValueNames().Take(256))
                    {
                        token.ThrowIfCancellationRequested();
                        if (registered.GetValue(name) is not string capabilities || string.IsNullOrWhiteSpace(capabilities)) continue;
                        foreach (var kind in new[] { "FileAssociations", "URLAssociations" })
                        {
                            using var associations = root.OpenSubKey(capabilities + "\\" + kind);
                            if (associations is null) continue;
                            foreach (var extension in associations.GetValueNames().Take(128))
                            {
                                token.ThrowIfCancellationRequested();
                                if (associations.GetValue(extension) is not string progId || !seen.Add(progId)) continue;
                                using var type = classes.OpenSubKey(progId);
                                if (type?.GetValue("AppUserModelID") is not string id || string.IsNullOrWhiteSpace(id)) continue;
                                using var command = type.OpenSubKey(@"shell\open\command");
                                if (command?.GetValue(null) is not string value) continue;
                                var executable = CommandExecutable(Environment.ExpandEnvironmentVariables(value));
                                if (!paths.Contains(executable)) continue;
                                if (!targets.TryGetValue(id, out var matches)) targets[id] = matches = new(StringComparer.OrdinalIgnoreCase);
                                matches.Add(executable);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException) { }
            }
        }
        var idsPerExecutable = targets.Values.SelectMany(matches => matches).ToLookup(path => path, StringComparer.OrdinalIgnoreCase);
        return targets.Select(pair => (Id: pair.Key, Target: ResolveRegisteredTarget(pair.Value, idsPerExecutable)))
            .Where(pair => pair.Target is not null).ToLookup(pair => pair.Target!, pair => pair.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveRegisteredTarget(HashSet<string> candidates, ILookup<string, string> idsPerExecutable)
    {
        if (candidates.Count == 1) return candidates.Single();
        // A suite can register the same module ID for its common launcher and a module EXE.
        if (candidates.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) return null;
        var shared = candidates.Where(path => idsPerExecutable[path].Skip(1).Any()).Take(2).ToArray();
        return shared.Length == 1 ? shared[0] : null;
    }

    private static string CommandExecutable(string command)
    {
        command = command.TrimStart();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : "";
        }
        var space = command.IndexOfAny([' ', '\t']);
        return space < 0 ? command : command[..space];
    }

    private static bool IsLocalExecutable(string path) => Path.IsPathFullyQualified(path)
        && !path.StartsWith(@"\\", StringComparison.Ordinal) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)
        && new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Network && File.Exists(path);

    private static bool IsMaintenanceExecutable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return new[] { "setup", "install", "unins", "update", "crash", "卸载", "安装" }.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))
            || new[] { "rundll32", "dllhost", "conhost", "msiexec", "werfault", "applicationframehost", "unopkg" }.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static string ExecutableTitle(string path)
    {
        var version = FileVersionInfo.GetVersionInfo(path);
        return !string.IsNullOrWhiteSpace(version.FileDescription) ? version.FileDescription.Trim()
            : !string.IsNullOrWhiteSpace(version.ProductName) ? version.ProductName.Trim() : Path.GetFileNameWithoutExtension(path);
    }
    private static void ReadUsedExecutables(Dictionary<string, SoftwareCandidate> found,
        IReadOnlyDictionary<string, int> usage, CancellationToken token)
    {
        // Portable apps may have activity evidence without any Start-menu shortcut.
        var known = found.Values.Select(c => c.Executable).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
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
                if (known.Contains(path) || IsMaintenanceExecutable(path) || !IsLocalExecutable(path)) continue;
                if (found.TryAdd(path, new(ExecutableTitle(path), path, path)) && ++added == 128) break;
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
