using System.IO;
using System.Windows;

namespace IStart;

public partial class MainWindow
{
    private static readonly Guid WebsitesCategory = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private bool websiteView;
    private bool readingWebsites;
    private bool readWebsiteSources = true;
    internal WebsiteHistoryResult websiteHistory = new([], 0, 0);
    private readonly HashSet<string> hiddenWebsites = new(StringComparer.OrdinalIgnoreCase);

    private async Task RefreshWebsiteHistory()
    {
        if (readingWebsites || !readWebsiteSources || !store.Library.ShowBrowserHistory) return;
        readingWebsites = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var result = await Task.Run(() => WebsiteHistory.Read(timeout.Token), timeout.Token).WaitAsync(timeout.Token);
            if (!store.Library.ShowBrowserHistory) return;
            websiteHistory = result;
            RuntimeLog.Write($"website-history profiles={result.ReadProfiles} failed={result.FailedProfiles} records={result.Visits.Count}");
            if (websiteView) RefreshEntries();
        }
        catch (OperationCanceledException) { if (websiteView) SearchStatus.Text = L.T("浏览器历史读取超时；已读记录仍可使用，重新打开此页重试。"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { if (websiteView) SearchStatus.Text = L.T("暂时无法读取浏览器历史；重新打开此页重试。"); }
        finally { readingWebsites = false; }
    }

    private void ToggleActivitySort(object sender, RoutedEventArgs e)
        => Perform(() => store.Change(s =>
        {
            if (folderView) s.FoldersRecentFirst = !s.FoldersRecentFirst;
            else s.WebsitesRecentFirst = !s.WebsitesRecentFirst;
        }), L.T("显示顺序已切换并保存。"));
}
