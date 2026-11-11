using System.IO;
using System.Windows.Controls;

namespace IStart;

internal static partial class ReliabilityTest
{
    private static async Task VerifyScheduledActivity(string output)
    {
        var morning = new DateTime(2026, 11, 11, 10, 0, 0);
        Check(ActivityCache.LatestSlot(morning) == morning.Date.AddDays(-1).AddHours(18), "morning uses the previous evening slot");
        Check(ActivityCache.LatestSlot(morning.Date.AddHours(12.5)) == morning.Date.AddHours(12.5), "12:30 is an update boundary");
        Check(ActivityCache.LatestSlot(morning.Date.AddHours(17.99)) == morning.Date.AddHours(12.5), "before 18:00 retains the noon slot");
        Check(ActivityCache.LatestSlot(morning.Date.AddHours(18)) == morning.Date.AddHours(18), "18:00 is an update boundary");
        var directory = Path.Combine(output, "scheduled-cache");
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "cached-app.exe");
        File.WriteAllText(executable, "fixture");
        var store = new LibraryStore(Path.Combine(directory, "library.json"));
        var window = new MainWindow(store, false);
        var refreshTime = (TextBlock)window.FindName("ActivityRefreshTime");
        Check(refreshTime.Text == L.T("上次刷新：尚未记录"), "missing refresh metadata must not invent a time");
        var reads = 0;
        Task<bool[]> Read()
        {
            reads++;
            window.candidates = [new("Saved App", executable, executable)];
            window.softwareUsage = new(StringComparer.OrdinalIgnoreCase) { [executable] = reads };
            return Task.FromResult(new[] { true, true, true });
        }
        try
        {
            await window.RefreshScheduledActivity(morning, Read);
            var firstTime = new ActivityCache(directory).Load()?.LastRefreshedAt;
            Check(firstTime is not null && refreshTime.Text == L.F("上次刷新：{0:MM-dd HH:mm}", firstTime.Value.LocalDateTime),
                "a successful persisted refresh displays its actual local completion time");
            Check(reads == 1 && new ActivityCache(directory).Load()?.Apps.Single().Title == "Saved App"
                && ((ListBox)window.FindName("EntryList")).Items.Count == 1 && !window.IsVisible,
                "first bootstrap must persist usable software while the menu is hidden");
            await window.RefreshScheduledActivity(morning.AddMinutes(10), Read);
            window.ShowMenu(null);
            await Task.Delay(300);
            window.Hide();
            Check(reads == 1, "opening and reopening before the next slot must not read activity sources");
            await window.RefreshScheduledActivity(morning.Date.AddHours(12.5), Read);
            await window.RefreshScheduledActivity(morning.Date.AddHours(17), Read);
            Check(reads == 2, "only the noon update runs during the noon slot");
            var savedTime = new ActivityCache(directory).Load()!.LastRefreshedAt
                ?? throw new InvalidOperationException("successful refresh time missing");
            await window.RefreshScheduledActivity(morning.Date.AddHours(18), () => Task.FromResult(new[] { true, false, true }));
            Check(new ActivityCache(directory).Load()!.LastRefreshedAt == savedTime,
                "a partially successful update must retain the last complete refresh time");
            await window.RefreshScheduledActivity(morning.Date.AddDays(1).AddHours(12.5), () =>
            {
                window.candidates = [];
                return Task.FromException<bool[]>(new IOException("fixture source failure"));
            });
            Check(window.candidates.Single().Title == "Saved App" && new ActivityCache(directory).Load()?.Apps.Single().Title == "Saved App",
                "a failed update retains both the in-memory and persisted successful snapshot");
            Check(new ActivityCache(directory).Load()!.LastRefreshedAt == savedTime
                && refreshTime.Text == L.F("上次刷新：{0:MM-dd HH:mm}", savedTime.LocalDateTime),
                "a failed update must retain the displayed and persisted refresh time");
            var blockedTemporary = Path.Combine(directory, "activity-cache.json.tmp");
            Directory.CreateDirectory(blockedTemporary);
            try { await window.RefreshScheduledActivity(morning.Date.AddDays(1).AddHours(18), Read); }
            finally { Directory.Delete(blockedTemporary); }
            Check(new ActivityCache(directory).Load()!.LastRefreshedAt == savedTime
                && refreshTime.Text == L.F("上次刷新：{0:MM-dd HH:mm}", savedTime.LocalDateTime),
                "a failed cache write must not advance the refresh time");
        }
        finally { window.Close(); }
        var restored = new MainWindow(new LibraryStore(Path.Combine(directory, "library.json")), false);
        try
        {
            Check(restored.candidates.Single().Title == "Saved App"
                && ((ListBox)restored.FindName("EntryList")).Items.Count == 1, "restart must restore the list before any source read");
            Check(((TextBlock)restored.FindName("ActivityRefreshTime")).Text
                == L.F("上次刷新：{0:MM-dd HH:mm}", new ActivityCache(directory).Load()!.LastRefreshedAt!.Value.LocalDateTime),
                "restart restores the saved refresh time without reading activity");
        }
        finally { restored.Close(); }
        File.WriteAllText(Path.Combine(directory, "activity-cache.json"), "{invalid");
        Check(new ActivityCache(directory).Load()?.Apps.Single().Title == "Saved App", "a damaged snapshot restores the saved backup");
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "activity-cache.json.bak")))!.AsObject();
        legacy.Remove("LastRefreshedAt");
        File.WriteAllText(Path.Combine(directory, "activity-cache.json"), legacy.ToJsonString());
        Check(new ActivityCache(directory).Load() is { LastRefreshedAt: null, Apps.Count: 1 },
            "an older cache without a refresh time still restores its activity");
        new ActivityCache(directory).Clear();
        Check(!File.Exists(Path.Combine(directory, "activity-cache.json")) && !File.Exists(Path.Combine(directory, "activity-cache.json.bak")),
            "clearing activity removes the current and backup caches");
        var sites = WebsiteHistory.ForCache(new([
            new("https://example.com/private?token=fixture", "Site", 3, DateTimeOffset.UtcNow),
            new("https://user:secret@example.com/", "Rejected", 1, DateTimeOffset.UtcNow)], 1, 0));
        Check(sites.Visits.Single().Url == "https://example.com/", "persisted website data keeps the domain and excludes credentials and query strings");
        File.AppendAllText(Path.Combine(output, "window-result.txt"),
            "PASS: scheduled 12:30/18:00 updates, hidden bootstrap, cached reopening, restart restoration, failed-update retention, corrupt-cache recovery and cache clearing; actual refresh time persists and survives partial/read/write failures, with legacy-cache compatibility.\n");
    }
}
