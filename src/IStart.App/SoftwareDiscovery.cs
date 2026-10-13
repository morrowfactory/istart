using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal sealed record SoftwareCandidate(string Title, string Shortcut);

internal static class SoftwareDiscovery
{
    public static List<SoftwareCandidate> Read(CancellationToken token, IEnumerable<string>? roots = null)
    {
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
                    found.TryAdd(Path.GetFullPath(target), new(title, shortcut));
                }
                catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
            }
        }
        return found.Values.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
