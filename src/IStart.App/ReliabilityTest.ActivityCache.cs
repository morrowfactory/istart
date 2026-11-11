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
            await window.RefreshScheduledActivity(morning.Date.AddHours(18), () =>
            {
                window.candidates = [];
                return Task.FromException<bool[]>(new IOException("fixture source failure"));
            });
            Check(window.candidates.Single().Title == "Saved App" && new ActivityCache(directory).Load()?.Apps.Single().Title == "Saved App",
                "a failed update retains both the in-memory and persisted successful snapshot");
        }
        finally { window.Close(); }
        var restored = new MainWindow(new LibraryStore(Path.Combine(directory, "library.json")), false);
        try
        {
            Check(restored.candidates.Single().Title == "Saved App"
                && ((ListBox)restored.FindName("EntryList")).Items.Count == 1, "restart must restore the list before any source read");
        }
        finally { restored.Close(); }
        File.WriteAllText(Path.Combine(directory, "activity-cache.json"), "{invalid");
        Check(new ActivityCache(directory).Load()?.Apps.Single().Title == "Saved App", "a damaged snapshot restores the saved backup");
        new ActivityCache(directory).Clear();
        Check(!File.Exists(Path.Combine(directory, "activity-cache.json")) && !File.Exists(Path.Combine(directory, "activity-cache.json.bak")),
            "clearing activity removes the current and backup caches");
        var sites = WebsiteHistory.ForCache(new([
            new("https://example.com/private?token=fixture", "Site", 3, DateTimeOffset.UtcNow),
            new("https://user:secret@example.com/", "Rejected", 1, DateTimeOffset.UtcNow)], 1, 0));
        Check(sites.Visits.Single().Url == "https://example.com/", "persisted website data keeps the domain and excludes credentials and query strings");
        File.AppendAllText(Path.Combine(output, "window-result.txt"),
            "PASS: scheduled 12:30/18:00 updates, hidden bootstrap, cached reopening, restart restoration, failed-update retention, corrupt-cache recovery and cache clearing.\n");
    }
}
