using System.IO;
using System.Windows.Threading;

namespace IStart;

public partial class MainWindow
{
    private readonly ActivityCache activityCache;
    private readonly DispatcherTimer activityTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
    private DateTime? attemptedActivitySlot;
    private bool activityRefreshing, activityBootstrapped;
    private int activityRevision;

    private void RestoreActivity(ActivitySnapshot snapshot)
    {
        candidates = snapshot.Apps;
        softwareUsage = new(snapshot.Usage, StringComparer.OrdinalIgnoreCase);
        windowsRecent = store.Library.ShowWindowsRecentDocuments ? snapshot.Files : [];
        recentFolders = store.Library.ShowWindowsRecentDocuments ? snapshot.Folders : [];
        websiteHistory = store.Library.ShowBrowserHistory ? snapshot.Websites : new([], 0, 0);
        applicationGroups = new();
    }

    private ActivitySnapshot CaptureActivity() => new()
    {
        AttemptedSlot = attemptedActivitySlot,
        Apps = candidates,
        Usage = softwareUsage,
        Files = store.Library.ShowWindowsRecentDocuments ? windowsRecent : [],
        Folders = store.Library.ShowWindowsRecentDocuments ? recentFolders : [],
        Websites = store.Library.ShowBrowserHistory ? websiteHistory : new([], 0, 0)
    };

    internal async Task RefreshScheduledActivity(DateTime now, Func<Task<bool[]>>? read = null)
    {
        if (menuClosed || activityRefreshing) return;
        var slot = ActivityCache.LatestSlot(now);
        var bootstrap = !activityBootstrapped && candidates.Count == 0;
        if (!bootstrap && attemptedActivitySlot >= slot) return;
        activityBootstrapped = true;
        activityRefreshing = true;
        var revision = activityRevision;
        var previous = CaptureActivity();
        attemptedActivitySlot = slot;
        try
        {
            // These readers share unfinished work and retain their last successful data on failure.
            var completed = read is null
                ? await Task.WhenAll(DiscoverSoftware(), RefreshRecentDocuments(), RefreshWebsiteHistory())
                : await read();
            if (menuClosed || revision != activityRevision) return;
            var snapshot = CaptureActivity();
            activityCache.Save(snapshot);
            RuntimeLog.Write($"activity-cache saved apps={candidates.Count} files={windowsRecent.Count} folders={recentFolders.Count} partial={completed.Any(ok => !ok)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException
            or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            if (revision == activityRevision) RestoreActivity(previous);
            RuntimeLog.Write("activity-cache-retained " + ex.GetType().Name);
        }
        finally
        {
            try
            {
                if (!menuClosed && !computerView)
                {
                    var viewRevision = entriesRevision;
                    var reader = CaptureEntryReader(true);
                    var entries = await Task.Run(reader);
                    if (!menuClosed && !computerView && viewRevision == entriesRevision) ApplyEntries(entries);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            { RuntimeLog.Write("activity-view-retained " + ex.GetType().Name); }
            finally { activityRefreshing = false; }
        }
    }

    private void ResetActivityCache(bool clearSoftware)
    {
        activityRevision++;
        activityCache.Clear();
        if (clearSoftware) { candidates = []; softwareUsage = new(StringComparer.OrdinalIgnoreCase); }
        if (clearSoftware || !store.Library.ShowWindowsRecentDocuments) { windowsRecent = []; recentFolders = []; }
        if (clearSoftware || !store.Library.ShowBrowserHistory) websiteHistory = new([], 0, 0);
        activityCache.Save(CaptureActivity());
    }
}
