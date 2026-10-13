using System.IO;
using System.Text.Json;

namespace IStart;

internal static class ActivityProbe
{
    internal static void Write(string output)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var candidates = SoftwareDiscovery.Read(timeout.Token);
        var usage = MySoftware.ReadUsage(timeout.Token);
        var sites = WebsiteHistory.Read(timeout.Token);
        var folders = FrequentFolders.Read(timeout.Token);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "activity-probe.json"), JsonSerializer.Serialize(new
        {
            SoftwareCandidates = candidates.Count,
            SoftwareWithUsage = MySoftware.Build([], candidates, [], usage, new HashSet<string>()).Count,
            BrowserProfiles = sites.ReadProfiles,
            FailedBrowserProfiles = sites.FailedProfiles,
            FrequentWebsites = WebsiteHistory.Build(sites.Visits, [], false, "", new HashSet<string>()).Count,
            RecentWebsites = WebsiteHistory.Build(sites.Visits, [], true, "", new HashSet<string>()).Count,
            RecentFolders = folders.Count
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
