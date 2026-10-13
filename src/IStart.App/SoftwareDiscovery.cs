using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal sealed record SoftwareCandidate(string Title, string Shortcut);

internal static class SoftwareDiscovery
{
    public static List<SoftwareCandidate> Read(CancellationToken token, IEnumerable<string>? roots = null)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return [];
        var shell = Activator.CreateInstance(shellType);
        if (shell is null) return [];
        var found = new Dictionary<string, SoftwareCandidate>(StringComparer.OrdinalIgnoreCase);
        try
        {
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
                    object? link = null;
                    try
                    {
                        link = ((dynamic)shell).CreateShortcut(shortcut);
                        string target = ((dynamic)link).TargetPath;
                        if (!string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) continue;
                        // Keep the original shortcut so application arguments and working directory survive.
                        found.TryAdd(Path.GetFullPath(target), new(title, shortcut));
                    }
                    catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException) { }
                    finally { if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); }
                }
            }
        }
        finally { if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        return found.Values.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
