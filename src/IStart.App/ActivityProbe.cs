using System.IO;
using System.Text.Json;

namespace IStart;

internal static class ActivityProbe
{
    internal static void Write(string output)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var usage = MySoftware.ReadUsage(timeout.Token);
        var candidates = SoftwareDiscovery.Read(timeout.Token, usage: usage);
        var failures = 0;
        var recentFolder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        List<LaunchRecord> Source(Func<List<LaunchRecord>> read)
            => RecentDocuments.ReadSources(timeout.Token, [read], () => failures++);
        var freeCad = Source(() => FreeCadDocuments.Read(timeout.Token));
        var libre = Source(() => LibreOfficeDocuments.Read(timeout.Token));
        var wps = Source(() => WpsDocuments.Read(timeout.Token));
        var jumpLists = Source(() => JumpListDocuments.Read(recentFolder, timeout.Token));
        var documents = RecentDocuments.Read(timeout.Token, failed: () => failures++);
        var apps = MySoftware.Build([], candidates, [], usage, new HashSet<string>());
        var sites = WebsiteHistory.Read(timeout.Token);
        var folders = Source(() => FrequentFolders.Read(timeout.Token));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "activity-probe.json"), JsonSerializer.Serialize(new
        {
            SoftwareCandidates = candidates.Count,
            SoftwareWithUsage = apps.Count,
            UsedExecutableCandidates = candidates.Count(c => c.Shortcut.Equals(c.Executable, StringComparison.OrdinalIgnoreCase)),
            FreeCadAppsShown = apps.Count(e => Path.GetFileName(e.Target).Equals("freecad.exe", StringComparison.OrdinalIgnoreCase)),
            FreeCadDocuments = freeCad.Count,
            FreeCadStepDocuments = freeCad.Count(r => new[] { ".stp", ".step" }.Contains(Path.GetExtension(r.Target), StringComparer.OrdinalIgnoreCase)),
            FreeCadDocumentsShown = documents.Count(r => freeCad.Any(f => f.Target.Equals(r.Target, StringComparison.OrdinalIgnoreCase))),
            LibreOfficeDocuments = libre.Count,
            WpsDocuments = wps.Count,
            JumpListDocuments = jumpLists.Count,
            HistoryReadFailures = failures,
            RecentDocuments = documents.Count,
            BrowserProfiles = sites.ReadProfiles,
            FailedBrowserProfiles = sites.FailedProfiles,
            FrequentWebsites = WebsiteHistory.Build(sites.Visits, [], false, "", new HashSet<string>()).Count,
            RecentWebsites = WebsiteHistory.Build(sites.Visits, [], true, "", new HashSet<string>()).Count,
            RecentFolders = folders.Count
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
