using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace IStart;

public partial class MainWindow
{
    private readonly DispatcherTimer refreshTimer = new(DispatcherPriority.ContextIdle) { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Func<Task>? refreshActivity;
    private bool refreshPending, sourcesPending, refreshingMenu, menuClosed;
    private int entriesRevision;

    private void PauseMenuRefresh()
    {
        refreshTimer.Stop();
        refreshPending = sourcesPending = false;
        entriesRevision++;
    }

    private void QueueMenuRefresh(bool readSources)
    {
        if (!IsVisible || menuClosed) return;
        refreshPending = true;
        sourcesPending |= readSources;
        if (!refreshingMenu) { refreshTimer.Stop(); refreshTimer.Start(); }
    }

    internal async Task RefreshVisibleContent()
    {
        refreshTimer.Stop();
        if (!IsVisible || menuClosed || refreshingMenu) return;
        refreshingMenu = true;
        var readSources = sourcesPending;
        var revision = entriesRevision;
        refreshPending = sourcesPending = false;
        try
        {
            // Opening only shows the retained visual tree. Reads start after render/input have had a turn.
            if (readSources)
            {
                if (refreshActivity is not null) await refreshActivity();
                else if (!await (computerView ? RefreshComputer()
                    : recentView || folderView ? RefreshRecentDocuments()
                    : websiteView ? RefreshWebsiteHistory() : DiscoverSoftware())) return;
            }
            if (!CanApply(revision)) { QueueMenuRefresh(false); return; }
            if (computerView)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (CanApply(revision)) RenderComputer();
                }, DispatcherPriority.ContextIdle);
                return;
            }
            var reader = CaptureEntryReader(true);
            // Existence checks, shortcut resolution, history merging, and ranking must not occupy the UI thread.
            var entries = await Task.Run(reader);
            await Dispatcher.InvokeAsync(() =>
            {
                if (CanApply(revision)) ApplyEntries(entries);
                else QueueMenuRefresh(false);
            }, DispatcherPriority.ContextIdle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or OperationCanceledException)
        {
            RuntimeLog.Write("menu-refresh-failed " + ex.GetType().Name);
            if (IsVisible && !menuClosed) StatusText.Text = L.T("内容刷新暂未完成，继续显示上次内容。");
        }
        finally
        {
            refreshingMenu = false;
            if (refreshPending && IsVisible && !menuClosed) refreshTimer.Start();
        }
    }

    private bool CanApply(int revision) => !menuClosed && IsVisible && !presentingMenu && revision == entriesRevision;

    private Func<List<MenuEntry>> CaptureEntryReader(bool background)
    {
        // Copy mutable state on the dispatcher; worker closures never read WPF controls or LibraryStore.
        var keyword = SearchBox.Text.Trim();
        var launches = store.Library.Launches.ToArray();
        Func<IEnumerable<MenuEntry>> read;
        if (recentView)
        {
            var external = store.Library.ShowWindowsRecentDocuments ? windowsRecent.ToArray() : [];
            read = () => RecentDocuments.Files(launches, external).Select(l => new MenuEntry(Guid.NewGuid(), l.Target, l.Title, Guid.Empty));
        }
        else if (softwareView)
        {
            var pinned = store.Library.Entries.ToArray();
            var apps = candidates.ToArray();
            var usage = new Dictionary<string, int>(softwareUsage, StringComparer.OrdinalIgnoreCase);
            var hidden = store.Library.HiddenSoftware.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var groups = background ? new EntryGroups() : applicationGroups;
            read = () => MySoftware.Build(pinned, apps, launches, usage, hidden, keyword, groups);
        }
        else if (websiteView)
        {
            var visits = store.Library.ShowBrowserHistory ? websiteHistory.Visits.ToArray() : [];
            var recent = store.Library.WebsitesRecentFirst;
            var hidden = new HashSet<string>(hiddenWebsites, StringComparer.OrdinalIgnoreCase);
            read = () => WebsiteHistory.Build(visits, launches, recent, keyword, hidden);
        }
        else
        {
            var pinned = store.Library.Entries.ToArray();
            var external = store.Library.ShowWindowsRecentDocuments ? windowsRecent.Concat(recentFolders).ToArray() : [];
            var columns = (int)FindResource("AppColumns");
            var rows = Math.Max(1, (int)(EntryList.ActualHeight / (double)FindResource("TileHeight")));
            var capacity = keyword.Length > 0 ? 100 : Math.Clamp(columns * rows, 1, 48);
            var recent = store.Library.FoldersRecentFirst;
            var hidden = new HashSet<string>(hiddenFolders, StringComparer.OrdinalIgnoreCase);
            read = () => FrequentFolders.Build(pinned, launches, external, capacity, Guid.Empty, hidden, recent, keyword);
        }
        return () => read().Where(e => e.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || e.Target.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static ScrollViewer? FindEntryScroll(DependencyObject parent)
    {
        if (parent is ScrollViewer scroll) return scroll;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindEntryScroll(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
}
