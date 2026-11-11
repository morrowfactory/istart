using System.IO;
using System.Windows;

namespace IStart;

public partial class MainWindow
{
    private static readonly Guid WebsitesCategory = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private bool websiteView;
    private bool readingWebsites;
    private Task<WebsiteHistoryResult>? websiteRead;
    private bool readWebsiteSources = true;
    internal WebsiteHistoryResult websiteHistory = new([], 0, 0);
    private readonly HashSet<string> hiddenWebsites = new(StringComparer.OrdinalIgnoreCase);

    private async Task<bool> RefreshWebsiteHistory()
    {
        if (readingWebsites) return false;
        if (!readWebsiteSources || !store.Library.ShowBrowserHistory) return true;
        readingWebsites = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(activityRefreshing ? 30 : 5));
        var token = timeout.Token;
        var revision = activityRevision;
        try
        {
            if (websiteRead is { IsFaulted: true } or { IsCanceled: true }) websiteRead = null;
            websiteRead ??= Task.Run(() => WebsiteHistory.Read(token), token);
            var result = await websiteRead.WaitAsync(token);
            if (!store.Library.ShowBrowserHistory || revision != activityRevision) return false;
            if ((result.FailedProfiles > 0 || result.Visits.Count == 0) && websiteHistory.Visits.Count > 0) return false;
            websiteHistory = result;
            RuntimeLog.Write($"website-history profiles={result.ReadProfiles} failed={result.FailedProfiles} records={result.Visits.Count}");
            return true;
        }
        catch (OperationCanceledException) { if (websiteView) SearchStatus.Text = L.T("更新未完成，继续显示上次保存的内容。"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { if (websiteView) SearchStatus.Text = L.T("更新未完成，继续显示上次保存的内容。"); }
        finally { readingWebsites = false; if (websiteRead?.IsCompleted == true) websiteRead = null; }
        return false;
    }

    private void ToggleActivitySort(object sender, RoutedEventArgs e)
        => Perform(() => store.Change(s =>
        {
            if (folderView) s.FoldersRecentFirst = !s.FoldersRecentFirst;
            else s.WebsitesRecentFirst = !s.WebsitesRecentFirst;
        }), L.T("显示顺序已切换并保存。"));
}
