using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace IStart;

public partial class MainWindow : Window
{
    private readonly LibraryStore store;
    private bool ready;
    internal List<SoftwareCandidate> candidates = [];
    internal Dictionary<string, int> softwareUsage = new(StringComparer.OrdinalIgnoreCase);
    private bool softwareView => !recentView && !folderView && !websiteView;
    private CancellationTokenSource? discovery;
    private bool recentView;
    private bool folderView;
    private List<LaunchRecord> windowsRecent = [];
    private List<LaunchRecord> recentFolders = [];
    private readonly HashSet<string> hiddenFolders = new(StringComparer.OrdinalIgnoreCase);
    private bool readingRecent;
    private static readonly Guid AllCategory = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid RecentCategory = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid FoldersCategory = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public MainWindow(LibraryStore store, bool enableShell = true)
    {
        this.store = store;
        readWebsiteSources = enableShell;
        Appearance.Apply(store.Library.Theme);
        InitializeComponent();
        ready = true;
        ApplyView();
        RefreshCategories();
        EntryList.SizeChanged += (_, _) => { if (folderView) RefreshEntries(); };
        if (store.Recovered) StatusText.Text = "主配置损坏，已读取上一份配置。请核对收藏后再修改。";
        Loaded += async (_, _) => await DiscoverSoftware();
        Closed += (_, _) => discovery?.Cancel();
        if (enableShell) InitializeShell();
    }

    private async Task DiscoverSoftware()
    {
        discovery?.Cancel();
        var current = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        discovery = current;
        try
        {
            var result = await Task.Run(() => (Apps: SoftwareDiscovery.Read(current.Token), Usage: MySoftware.ReadUsage(current.Token)), current.Token).WaitAsync(current.Token);
            if (discovery != current) return;
            candidates = result.Apps;
            softwareUsage = result.Usage;
            RefreshEntries();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "软件整理超时；已添加的软件仍可使用，重新打开菜单会重试。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { StatusText.Text = "暂时无法读取软件使用记录，已添加的软件仍可使用。"; }
    }
    private void RefreshCategories()
    {
        CategoryList.ItemsSource = new[] { new MenuCategory(AllCategory, "我的软件"), new(RecentCategory, "最近打开"), new(FoldersCategory, "常用文件夹"), new(WebsitesCategory, "我的网站") };
        CategoryList.SelectedIndex = 0;
    }
    private void RefreshEntries()
    {
        if (!ready) return;
        var keyword = SearchBox.Text.Trim();
        IEnumerable<MenuEntry> source = recentView ? RecentDocuments.Files(store.Library.Launches,
            store.Library.ShowWindowsRecentDocuments ? windowsRecent : []).Select(l => new MenuEntry(Guid.NewGuid(), l.Target, l.Title, Guid.Empty)) : store.Library.Entries;
        if (softwareView) source = MySoftware.Build(store.Library.Entries, candidates, store.Library.Launches, softwareUsage,
            store.Library.HiddenSoftware.ToHashSet(StringComparer.OrdinalIgnoreCase), keyword);
        if (websiteView) source = WebsiteHistory.Build(store.Library.ShowBrowserHistory ? websiteHistory.Visits : [],
            store.Library.Launches, store.Library.WebsitesRecentFirst, keyword, hiddenWebsites);
        if (folderView)
        {
            var columns = Math.Max(1, (int)(EntryList.ActualWidth / ((double)FindResource("TileWidth") + 10)));
            var rows = Math.Max(1, (int)(EntryList.ActualHeight / ((double)FindResource("TileHeight") + 18)));
            var capacity = keyword.Length > 0 ? 100 : Math.Clamp(columns * rows, 1, 48);
            source = FrequentFolders.Build(store.Library.Entries,
                store.Library.Launches, store.Library.ShowWindowsRecentDocuments ? windowsRecent.Concat(recentFolders) : [],
                capacity, Guid.Empty, hiddenFolders, store.Library.FoldersRecentFirst);
        }
        var entries = source.Where(e => (e.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || e.Target.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
        var view = new System.Windows.Data.ListCollectionView(entries);
        EntryList.ItemsSource = view;
        ApplyView();
        ActivitySortButton.Content = folderView ? store.Library.FoldersRecentFirst ? "最近打开 · 切换常用" : "常用文件夹 · 切换最近"
            : store.Library.WebsitesRecentFirst ? "最近打开 · 切换常用" : "常用网站 · 切换最近";
        if (recentView) SearchStatus.Text = EntryList.Items.Count == 0 ? "暂无有效记录。仅显示软件或 Windows 已保存、且仍存在的本机文件。" : "合并 Windows、软件历史与 iStart 记录；外部历史时间为近似排序。";
        else SearchStatus.Text = websiteView ? !store.Library.ShowBrowserHistory ? "浏览器历史已关闭；仅显示通过 iStart 打开的网站。"
            : websiteHistory.FailedProfiles > 0 ? "部分浏览器历史暂不可读；显示可读取记录，可重新打开此页重试。"
            : store.Library.WebsitesRecentFirst ? "Chrome 与 Edge 合并，按最后打开时间排序；同域名显示一项。" : "Chrome 与 Edge 合并，按累计访问次数排序；同域名显示一项。"
            : folderView ? store.Library.FoldersRecentFirst ? "按文件夹最近打开时间排序；外部历史时间可能近似。"
            : "按文件夹打开次数和近期文件活动排序；无完整系统频率统计。"
            : "按本机使用活动排序，最多 30 个软件。";
        if (softwareView && entries.Count == 0 && keyword.Length == 0) SearchStatus.Text = "暂无可用软件活动记录；可以搜索或添加常用软件。";
    }
    private async Task RefreshRecentDocuments()
    {
        if (readingRecent || !store.Library.ShowWindowsRecentDocuments) return;
        readingRecent = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var records = await Task.Run(() => (Files: RecentDocuments.Read(timeout.Token), Folders: FrequentFolders.Read(timeout.Token)), timeout.Token).WaitAsync(timeout.Token);
            if (!store.Library.ShowWindowsRecentDocuments) return;
            windowsRecent = records.Files;
            recentFolders = records.Folders;
            if (recentView || folderView) RefreshEntries();
        }
        catch (OperationCanceledException) { if (recentView) SearchStatus.Text = "Windows 最近文档读取超时；iStart 打开记录仍可使用。"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (recentView) SearchStatus.Text = "暂时无法读取 Windows 最近文档；iStart 打开记录仍可使用。"; }
        finally { readingRecent = false; }
    }
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!ready) return;
        RefreshEntries();
        var searching = SearchBox.Text.Trim().Length > 0;
        SectionTitle.Text = searching ? recentView ? "最近记录搜索结果" : "搜索结果" : (CategoryList.SelectedItem as MenuCategory)?.Name ?? "我的软件";
        if (softwareView && searching && EntryList.Items.Count == 0) SearchStatus.Text = "没有匹配的软件。";
    }
    private void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not MenuCategory category) return;
        recentView = category.Id == RecentCategory;
        websiteView = category.Id == WebsitesCategory;
        folderView = category.Id == FoldersCategory || category.Name == "常用文件夹";
        SectionTitle.Text = category.Name;
        AddFileButton.Content = softwareView ? "＋ 软件" : "＋ 文件";
        AddFileButton.Visibility = folderView || websiteView ? Visibility.Collapsed : Visibility.Visible;
        AddFolderButton.Visibility = folderView ? Visibility.Visible : Visibility.Collapsed;
        ActivitySortButton.Visibility = websiteView || folderView ? Visibility.Visible : Visibility.Collapsed;
        ViewToggle.Visibility = websiteView ? Visibility.Collapsed : Visibility.Visible;
        RefreshEntries();
        ApplyView();
        if (recentView || folderView) _ = RefreshRecentDocuments();
        if (websiteView) _ = RefreshWebsiteHistory();
    }
    private void Perform(Action action, string success)
    {
        try { action(); RefreshEntries(); StatusText.Text = success; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { StatusText.Text = "操作未完成，请检查目标或配置文件权限。原文件未被移动。"; }
    }
    private void AddFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = softwareView ? "添加常用软件" : "打开文件", Filter = softwareView ? "软件|*.exe;*.lnk;*.appref-ms|全部文件|*.*" : "全部文件|*.*" };
        if (dialog.ShowDialog(this) == true)
        {
            if (softwareView) AddPaths(dialog.FileNames, Guid.Empty, null);
            else foreach (var file in dialog.FileNames) OpenTarget(file);
        }
    }
    private void AddPaths(string[] paths, Guid category, Guid? before)
    {
        var groups = new EntryGroups();
        var accepted = paths.Where(path => folderView ? Directory.Exists(path)
            : !softwareView || groups.IsApplication(new(Guid.Empty, path, "", Guid.Empty))).ToArray();
        if (accepted.Length == 0) { StatusText.Text = folderView ? "请拖入文件夹。" : "请拖入软件或应用快捷方式。"; return; }
        Perform(() => store.AddPaths(accepted, category, before), "入口已添加；当前列表按使用活动排序。");
        if (recentView) CategoryList.SelectedIndex = 0;
    }
    private static T? ItemAt<T>(ItemsControl list, object source) where T : class
    {
        var element = source as DependencyObject;
        while (element is not null)
        {
            if (element is ListBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) == list) return item.DataContext as T;
            element = ParentOf(element);
        }
        return null;
    }
    private static bool IsButton(object source)
    {
        var element = source as DependencyObject;
        while (element is not null)
        {
            if (element is Button) return true;
            element = ParentOf(element);
        }
        return false;
    }
    private static DependencyObject? ParentOf(DependencyObject element) => element is FrameworkContentElement content
        ? content.Parent : VisualTreeHelper.GetParent(element);
    private void EntryDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !recentView && !websiteView && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }
    private void EntryDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!recentView && !websiteView && e.Data.GetDataPresent(DataFormats.FileDrop)) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), Guid.Empty, null);
    }
    private void RemoveEntry(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) RemoveById(id);
        e.Handled = true;
    }
    private void OpenSelected(object sender, MouseButtonEventArgs e)
    {
        var entry = ItemAt<MenuEntry>(EntryList, e.OriginalSource);
        if (entry is null || IsButton(e.OriginalSource)) return;
        if (!websiteView && !entry.Target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal) && !File.Exists(entry.Target) && !Directory.Exists(entry.Target)) { StatusText.Text = "目标已移动、删除或暂时离线，可右键重新定位，或移除此入口。"; return; }
        OpenTarget(entry.Target, entry.Title);
    }
    private void OpenTarget(string target, string? title = null)
    {
        try
        {
            if (target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal))
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { target } });
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            StatusText.Text = "已交给 Windows 打开。";
            try { store.RecordLaunch(target, title ?? Path.GetFileName(target)); RefreshEntries(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = "已打开，但本次启动记录暂时无法保存。"; }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { StatusText.Text = "Windows 未能打开此入口，请检查文件关联或目标是否可用。"; }
    }
    private void OpenNative(object sender, RoutedEventArgs e)
    {
        Hide();
        Dispatcher.BeginInvoke(() =>
        {
            try { NativeMenu.Open(); }
            catch (System.ComponentModel.Win32Exception) { Show(); StatusText.Text = "Windows 暂未响应，请直接点击开始按钮或切回原生模式。"; }
        });
    }
    private void Exit(object sender, RoutedEventArgs e) => RequestExit("menu");
}
