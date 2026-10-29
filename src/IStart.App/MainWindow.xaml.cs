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
    internal UpdateService Updates { get; }
    private bool ready;
    internal List<SoftwareCandidate> candidates = [];
    internal Dictionary<string, int> softwareUsage = new(StringComparer.OrdinalIgnoreCase);
    private EntryGroups applicationGroups = new();
    private bool softwareView => !recentView && !folderView && !websiteView && !computerView;
    private CancellationTokenSource? discovery;
    private Task? discoveryTask;
    private Task<(List<SoftwareCandidate> Apps, Dictionary<string, int> Usage)>? softwareRead;
    private long softwareRefreshedAt;
    private bool recentView;
    private bool folderView;
    private List<LaunchRecord> windowsRecent = [];
    private List<LaunchRecord> recentFolders = [];
    private readonly HashSet<string> hiddenFolders = new(StringComparer.OrdinalIgnoreCase);
    private bool readingRecent;
    private int recentReadFailures;
    private static readonly Guid AllCategory = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid RecentCategory = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid FoldersCategory = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public MainWindow(LibraryStore store, bool enableShell = true) : this(store, enableShell, null) { }
    internal MainWindow(LibraryStore store, bool enableShell, Func<CancellationToken, Task<AppRelease?>>? checkUpdates)
    {
        this.store = store;
        Updates = new UpdateService(store, () => UpdateInstallation.IsInstalled && !App.IsSessionEnding && !exitRequested
            && !capturing && !IsVisible && OwnedWindows.Count == 0 && UpdateInstallation.IsIdle(), checkUpdates);
        readWebsiteSources = enableShell;
        Appearance.Apply(store.Library.Theme);
        L.Apply(store.Library.Language);
        InitializeComponent();
        ApplyThemeLayout();
        Updates.Changed += RefreshUpdateIndicators;
        ready = true;
        ApplyView();
        RefreshCategories();
        EntryList.SizeChanged += (_, _) => { if (folderView) RefreshEntries(); };
        if (store.Recovered) StatusText.Text = L.T("主配置损坏，已读取上一份配置。请核对收藏后再修改。");
        Loaded += async (_, _) => await DiscoverSoftware();
        Closed += (_, _) => { discovery?.Cancel(); Updates.Dispose(); };
        if (enableShell) Loaded += (_, _) => { if (UpdateInstallation.IsInstalled) Updates.Start(); };
        if (enableShell) InitializeShell();
    }

    private void RefreshUpdateIndicators()
    {
        if (!Dispatcher.CheckAccess()) { if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(RefreshUpdateIndicators); return; }
        SettingsUpdateBadge.Visibility = Updates.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        SettingsButton.ToolTip = Updates.Release is { } release ? L.F("发现新版 {0}，进入设置查看更新内容。", release.Version) : L.T("设置");
        System.Windows.Automation.AutomationProperties.SetHelpText(SettingsButton, SettingsButton.ToolTip.ToString());
    }

    internal Task DiscoverSoftware()
    {
        if (discoveryTask is { IsCompleted: false }) return discoveryTask;
        if (softwareRefreshedAt != 0 && Environment.TickCount64 - softwareRefreshedAt < 30000) return Task.CompletedTask;
        return discoveryTask = ReadSoftware();
    }
    private async Task ReadSoftware()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var current = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        discovery = current;
        var token = current.Token;
        try
        {
            softwareRead ??= Task.Run(() =>
            {
                var usage = MySoftware.ReadUsage(token);
                return (Apps: SoftwareDiscovery.Read(token, usage: usage), Usage: usage);
            }, token);
            var result = await softwareRead.WaitAsync(token);
            if (discovery != current) return;
            candidates = result.Apps;
            softwareUsage = result.Usage;
            applicationGroups = new();
            softwareRefreshedAt = Environment.TickCount64;
            RuntimeLog.Write($"software-refresh count={candidates.Count} elapsedMs={started.ElapsedMilliseconds}");
            if (softwareView) RefreshEntries();
        }
        catch (OperationCanceledException)
        {
            if (discovery == current) StatusText.Text = L.T("软件整理超时；已添加的软件仍可使用，重新打开菜单会重试。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { if (discovery == current) StatusText.Text = L.T("暂时无法读取软件使用记录，已添加的软件仍可使用。"); }
        finally
        {
            if (softwareRead?.IsCompleted == true) softwareRead = null;
            if (discovery == current) discovery = null;
            current.Dispose();
        }
    }
    private void RefreshCategories()
    {
        var selected = (CategoryList.SelectedItem as MenuCategory)?.Id ?? AllCategory;
        CategoryList.ItemsSource = new[] { new MenuCategory(AllCategory, L.T("我的软件")), new(RecentCategory, L.T("最近打开")), new(FoldersCategory, L.T("常用文件夹")), new(WebsitesCategory, L.T("我的网站")), new(ComputerCategory, L.T("我的电脑")) };
        CategoryList.SelectedItem = CategoryList.Items.OfType<MenuCategory>().FirstOrDefault(c => c.Id == selected);
    }
    private void RefreshEntries()
    {
        if (!ready) return;
        if (computerView) { RenderComputer(); return; }
        var keyword = SearchBox.Text.Trim();
        IEnumerable<MenuEntry> source = recentView ? RecentDocuments.Files(store.Library.Launches,
            store.Library.ShowWindowsRecentDocuments ? windowsRecent : []).Select(l => new MenuEntry(Guid.NewGuid(), l.Target, l.Title, Guid.Empty)) : store.Library.Entries;
        if (softwareView) source = MySoftware.Build(store.Library.Entries, candidates, store.Library.Launches, softwareUsage,
            store.Library.HiddenSoftware.ToHashSet(StringComparer.OrdinalIgnoreCase), keyword, applicationGroups);
        if (websiteView) source = WebsiteHistory.Build(store.Library.ShowBrowserHistory ? websiteHistory.Visits : [],
            store.Library.Launches, store.Library.WebsitesRecentFirst, keyword, hiddenWebsites);
        if (folderView)
        {
            var columns = (int)FindResource("AppColumns");
            var rows = Math.Max(1, (int)(EntryList.ActualHeight / (double)FindResource("TileHeight")));
            var capacity = keyword.Length > 0 ? 100 : Math.Clamp(columns * rows, 1, 48);
            source = FrequentFolders.Build(store.Library.Entries,
                store.Library.Launches, store.Library.ShowWindowsRecentDocuments ? windowsRecent.Concat(recentFolders) : [],
                capacity, Guid.Empty, hiddenFolders, store.Library.FoldersRecentFirst, keyword);
        }
        var entries = source.Where(e => (e.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || e.Target.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
        var view = new System.Windows.Data.ListCollectionView(entries);
        EntryList.ItemsSource = view;
        ApplyView();
        ActivitySortButton.ToolTip = folderView ? store.Library.FoldersRecentFirst ? L.T("最近打开 · 切换常用") : L.T("常用文件夹 · 切换最近")
            : store.Library.WebsitesRecentFirst ? L.T("最近打开 · 切换常用") : L.T("常用网站 · 切换最近");
        if (recentView) SearchStatus.Text = EntryList.Items.Count == 0 ? L.T("暂无有效记录。仅显示软件或 Windows 已保存、且仍存在的本机文件。") : L.T("合并 Windows、软件历史与 iStart 记录；外部历史时间为近似排序。");
        else SearchStatus.Text = websiteView ? !store.Library.ShowBrowserHistory ? L.T("浏览器历史已关闭；仅显示通过 iStart 打开的网站。")
            : websiteHistory.FailedProfiles > 0 ? L.T("部分浏览器历史暂不可读；显示可读取记录，可重新打开此页重试。")
            : store.Library.WebsitesRecentFirst ? L.T("Chrome 与 Edge 合并，按最后打开时间排序；同域名显示一项。") : L.T("Chrome 与 Edge 合并，按累计访问次数排序；同域名显示一项。")
            : folderView ? store.Library.FoldersRecentFirst ? L.T("按文件夹最近打开时间排序；外部历史时间可能近似。")
            : L.T("按文件夹打开次数和近期文件活动排序；无完整系统频率统计。")
            : L.T("按本机使用活动排序，最多 30 个软件。");
        if (softwareView && entries.Count == 0 && keyword.Length == 0) SearchStatus.Text = L.T("暂无可用软件活动记录；可以搜索或添加常用软件。");
        if ((recentView || folderView) && store.Library.ShowWindowsRecentDocuments && recentReadFailures > 0)
            SearchStatus.Text = L.T("部分文件或文件夹历史暂不可读；已显示其他可读取记录，可重新打开此页重试。");
    }
    private async Task RefreshRecentDocuments()
    {
        if (readingRecent || !store.Library.ShowWindowsRecentDocuments) return;
        readingRecent = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var records = await Task.Run(() =>
            {
                var failures = 0;
                var files = RecentDocuments.Read(timeout.Token, failed: () => failures++);
                var folders = RecentDocuments.ReadSources(timeout.Token, [() => FrequentFolders.Read(timeout.Token)], () => failures++);
                return (Files: files, Folders: folders, Failures: failures);
            }, timeout.Token).WaitAsync(timeout.Token);
            if (!store.Library.ShowWindowsRecentDocuments) return;
            windowsRecent = records.Files;
            recentFolders = records.Folders;
            recentReadFailures = records.Failures;
            if (recentView || folderView) RefreshEntries();
        }
        catch (OperationCanceledException) { if (recentView) SearchStatus.Text = L.T("Windows 最近文档读取超时；iStart 打开记录仍可使用。"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (recentView) SearchStatus.Text = L.T("暂时无法读取 Windows 最近文档；iStart 打开记录仍可使用。"); }
        finally { readingRecent = false; }
    }
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!ready) return;
        RefreshEntries();
        var searching = SearchBox.Text.Trim().Length > 0;
        SectionTitle.Text = searching ? recentView ? L.T("最近记录搜索结果") : L.T("搜索结果") : (CategoryList.SelectedItem as MenuCategory)?.Name ?? L.T("我的软件");
        if (softwareView && searching && EntryList.Items.Count == 0) SearchStatus.Text = L.T("没有匹配的软件。");
    }
    private void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not MenuCategory category) return;
        computerView = category.Id == ComputerCategory;
        recentView = category.Id == RecentCategory;
        websiteView = category.Id == WebsitesCategory;
        folderView = category.Id == FoldersCategory;
        SectionTitle.Text = category.Name;
        AddFileButton.ToolTip = softwareView ? L.T("添加常用软件") : L.T("打开文件");
        AddFileButton.Visibility = folderView || websiteView || computerView ? Visibility.Collapsed : Visibility.Visible;
        AddFolderButton.Visibility = folderView ? Visibility.Visible : Visibility.Collapsed;
        ActivitySortButton.Visibility = websiteView || folderView ? Visibility.Visible : Visibility.Collapsed;
        ViewToggle.Visibility = websiteView || computerView ? Visibility.Collapsed : Visibility.Visible;
        EntryList.Visibility = computerView ? Visibility.Collapsed : Visibility.Visible;
        ComputerScroll.Visibility = computerView ? Visibility.Visible : Visibility.Collapsed;
        EntryCount.Visibility = computerView ? Visibility.Collapsed : Visibility.Visible;
        RefreshEntries();
        ApplyView();
        if (computerView) _ = RefreshComputer();
        if (recentView || folderView) _ = RefreshRecentDocuments();
        if (websiteView) _ = RefreshWebsiteHistory();
        if (softwareView && IsLoaded) _ = DiscoverSoftware();
    }
    private void Perform(Action action, string success)
    {
        try { action(); RefreshEntries(); StatusText.Text = L.T(success); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { StatusText.Text = L.T("操作未完成，请检查目标或配置文件权限。原文件未被移动。"); }
    }
    private void AddFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = softwareView ? L.T("添加常用软件") : L.T("打开文件"), Filter = softwareView ? L.T("软件|*.exe;*.lnk;*.appref-ms|全部文件|*.*") : L.T("全部文件|*.*") };
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
        if (accepted.Length == 0) { StatusText.Text = folderView ? L.T("请拖入文件夹。") : L.T("请拖入软件或应用快捷方式。"); return; }
        Perform(() => store.AddPaths(accepted, category, before), L.T("入口已添加；当前列表按使用活动排序。"));
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
        e.Effects = !recentView && !websiteView && !computerView && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }
    private void EntryDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!recentView && !websiteView && !computerView && e.Data.GetDataPresent(DataFormats.FileDrop)) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), Guid.Empty, null);
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
        if (!websiteView && !entry.Target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal) && !File.Exists(entry.Target) && !Directory.Exists(entry.Target)) { StatusText.Text = L.T("目标已移动、删除或暂时离线，可右键重新定位，或移除此入口。"); return; }
        OpenTarget(entry.Target, entry.Title);
    }
    private void OpenTarget(string target, string? title = null)
    {
        try
        {
            if (target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal))
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { target } });
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            StatusText.Text = L.T("已交给 Windows 打开。");
            try { store.RecordLaunch(target, title ?? Path.GetFileName(target)); RefreshEntries(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = L.T("已打开，但本次启动记录暂时无法保存。"); }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { StatusText.Text = L.T("Windows 未能打开此入口，请检查文件关联或目标是否可用。"); }
    }
    private void OpenNative(object sender, RoutedEventArgs e)
    {
        Hide();
        Dispatcher.BeginInvoke(() =>
        {
            try { NativeMenu.Open(); }
            catch (System.ComponentModel.Win32Exception) { Show(); StatusText.Text = L.T("Windows 暂未响应，请直接点击开始按钮或切回原生模式。"); }
        });
    }
    private void Exit(object sender, RoutedEventArgs e) => RequestExit("menu");
}
