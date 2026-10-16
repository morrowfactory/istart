using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

internal static partial class SmokeTest
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var fixture = Path.Combine(output, "fixture");
        Directory.CreateDirectory(fixture);
        var file = Path.Combine(fixture, "中文 文档.txt");
        var second = Path.Combine(fixture, "第二项.txt");
        File.WriteAllText(file, "source must remain intact");
        File.WriteAllText(second, "second");
        var config = Path.Combine(fixture, "config", "library.json");
        var store = new LibraryStore(config);
        Check(store.AddPaths([file, second, fixture], Guid.Empty) == 3, "文件和文件夹添加");
        Check(store.AddPaths([file.ToUpperInvariant()], Guid.Empty) == 0, "重复项去重");
        var category = Guid.NewGuid();
        var another = Guid.NewGuid();
        store.Change(s => { s.Categories.Add(new(category, "工作")); s.Categories.Add(new(another, "个人")); });
        var entry = store.Library.Entries[1];
        store.Move(entry.Id, category, store.Library.Entries[0].Id);
        store.MoveCategory(another, category);
        var loaded = new LibraryStore(config);
        Check(loaded.Library.Entries[0].Id == entry.Id && loaded.Library.Entries[0].CategoryId == category, "顺序与分类持久化");
        Check(loaded.Library.Categories[1].Id == another, "分类排序持久化");
        loaded.Change(s => s.Entries.RemoveAll(e => e.Id == entry.Id));
        Check(File.ReadAllText(second) == "second", "移除不删除源文件");
        File.WriteAllText(config, "invalid json");
        Check(new LibraryStore(config).Recovered, "损坏配置从备份恢复");
        File.WriteAllText(config, "{\"SchemaVersion\":99}");
        try { _ = new LibraryStore(config); throw new Exception("不应覆盖未知配置版本"); }
        catch (NotSupportedException) { }

        // Persistence failure must leave the in-memory state unchanged.
        var blocked = Path.Combine(fixture, "blocked");
        File.WriteAllText(blocked, "not a directory");
        var failed = new LibraryStore(Path.Combine(blocked, "library.json"));
        try { failed.AddPaths([file], Guid.Empty); throw new Exception("预期保存失败"); }
        catch (IOException) { Check(failed.Library.Entries.Count == 0, "保存失败回退内存状态"); }

        VerifySoftwareDiscovery(fixture);
        VerifyWebsiteHistory(fixture);
        VerifyUpdater(fixture);
        VerifyCompatibility();
        VerifyScreenshot(output);
        Check(ComputerItems.IsControlPanelCategory(ComputerItems.ControlPanel[6..] + "\\5")
            && !ComputerItems.IsControlPanelCategory(ComputerItems.ControlPanel[6..] + "\\7")
            && !ComputerItems.IsControlPanelCategory(ComputerItems.ControlPanel[6..] + "\\0"), "控制面板仅允许七个大类，排除轻松使用及全部子项");
        var recentFolder = Path.Combine(fixture, "recent");
        Directory.CreateDirectory(recentFolder);
        ShellShortcut.CreateFixture(Path.Combine(recentFolder, "文档.lnk"), file);
        ShellShortcut.CreateFixture(Path.Combine(recentFolder, "重复.lnk"), file);
        ShellShortcut.CreateFixture(Path.Combine(recentFolder, "不存在.lnk"), Path.Combine(fixture, "missing.txt"));
        ShellShortcut.CreateFixture(Path.Combine(recentFolder, "目录.lnk"), fixture);
        var recentFiles = RecentDocuments.Read(CancellationToken.None, recentFolder);
        Check(recentFiles.Count == 1 && recentFiles[0].Target == file, "Windows 最近文档读取、去重和无效目标过滤");
        var libreConfig = Path.Combine(fixture, "history.xcu");
        File.WriteAllText(libreConfig, $"<items xmlns:oor=\"http://openoffice.org/2001/registry\"><item oor:path=\"/org.openoffice.Office.Histories/Histories/org.openoffice.Office.Histories:HistoryInfo['PickList']/OrderList\"><node oor:name=\"0\"><prop oor:name=\"HistoryItemRef\"><value>{System.Security.SecurityElement.Escape(new Uri(file).AbsoluteUri)}</value></prop></node></item></items>");
        Check(LibreOfficeDocuments.Read(CancellationToken.None, libreConfig).Single().Target == file, "LibreOffice 独立最近文档读取与 URI 解码");
        using (var recentTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var systemRecent = RecentDocuments.Read(recentTimeout.Token);
            File.WriteAllText(Path.Combine(output, "recent-sources.txt"), $"跨软件有效最近文件：{systemRecent.Count}；图片：{systemRecent.Count(r => new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }.Contains(Path.GetExtension(r.Target), StringComparer.OrdinalIgnoreCase))}；办公文档：{systemRecent.Count(r => new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp" }.Contains(Path.GetExtension(r.Target), StringComparer.OrdinalIgnoreCase))}");
            File.AppendAllText(Path.Combine(output, "recent-sources.txt"), $"；LibreOffice 有效条目：{LibreOfficeDocuments.Read(recentTimeout.Token).Count}；WPS 有效条目：{WpsDocuments.Read(recentTimeout.Token).Count}");
        }
        var ownRecent = new LaunchRecord(file, "自定义名称", DateTimeOffset.UtcNow.AddMinutes(1), 2);
        Check(RecentDocuments.Merge([ownRecent], recentFiles).Single() == ownRecent, "合并最近记录保留最新且去重");
        Check(RecentDocuments.Files([ownRecent, new(fixture, "目录", DateTimeOffset.UtcNow, 9),
            new(Path.Combine(fixture, "missing.txt"), "失效", DateTimeOffset.UtcNow, 1)], recentFiles).Single() == ownRecent,
            "最近文件过滤自身文件夹与失效记录，保留文件去重");
        Check(FrequentFolders.Read(CancellationToken.None, recentFolder).Single().Target == fixture,
            "Windows 文件夹历史独立读取，不混入最近文件");
        var highFolder = Path.Combine(fixture, "高频目录");
        var newFolder = Path.Combine(fixture, "新近目录");
        Directory.CreateDirectory(highFolder);
        Directory.CreateDirectory(newFolder);
        var pinnedFolder = new MenuEntry(Guid.NewGuid(), fixture, "手动固定", Guid.Empty);
        LaunchRecord[] folderOwn = [new(highFolder, "高频", DateTimeOffset.UtcNow.AddDays(-1), 12)];
        LaunchRecord[] folderExternal = [new(newFolder, "新近", DateTimeOffset.UtcNow, 1),
            new(highFolder, "重复", DateTimeOffset.UtcNow, 1), new(fixture, "已固定", DateTimeOffset.UtcNow.AddMinutes(1), 1)];
        var folderResults = FrequentFolders.Build([pinnedFolder], folderOwn, folderExternal, 3, Guid.Empty, new HashSet<string>());
        Check(folderResults.Count == 3 && folderResults[0].Target == highFolder && folderResults.Contains(pinnedFolder),
            "常用文件夹高频在前，固定入口保留且去重");
        Check(FrequentFolders.Build([pinnedFolder], folderOwn, folderExternal, 1, Guid.Empty, new HashSet<string>()).Single().Target == highFolder,
            "常用文件夹容量限制与频率排序");
        Check(FrequentFolders.Build([pinnedFolder], folderOwn, folderExternal, 3, Guid.Empty, new HashSet<string>(), recent: true)[0].Target == fixture,
            "最近文件夹按实际目录记录时间排序");
        Check(FrequentFolders.Build([pinnedFolder], folderOwn, folderExternal, 3, Guid.Empty, new HashSet<string> { highFolder }).All(e => e.Target != highFolder),
            "隐藏本次文件夹建议");
        store.Change(s => s.ShowWindowsRecentDocuments = false);
        Check(!new LibraryStore(config).Library.ShowWindowsRecentDocuments, "Windows 最近文档开关持久化");
        store.Change(s => s.ShowWindowsRecentDocuments = true);
        var work = new Rect(0, 0, 1920, 1032);
        var size = new Size(740, 660);
        var bottom = MenuPlacement.Calculate(work, new Rect(12, 1032, 48, 48), size);
        Check(bottom.Left == 12 && bottom.Bottom == 1024, "底部开始按钮定位，不跟随鼠标");
        Check(MenuPlacement.Calculate(new Rect(0, 48, 1920, 1032), new Rect(12, 0, 48, 48), size).Top == 56,
            "顶部任务栏定位");
        Check(MenuPlacement.Calculate(new Rect(48, 0, 1872, 1080), new Rect(0, 12, 48, 48), size).Left == 56,
            "左侧任务栏定位");
        var negative = new Rect(-1280, 0, 1280, 984);
        Check(negative.Contains(MenuPlacement.Calculate(negative, new Rect(-20, 984, 20, 40), size)),
            "负坐标屏幕边缘约束");
        store.Change(s => s.HiddenSoftware.Add("hidden-shortcut"));
        var preferences = new LibraryStore(config);
        Check(preferences.Library.HiddenSoftware.Contains("hidden-shortcut"), "软件隐藏项持久化");

        var upgrade = Path.Combine(fixture, "upgrade.json");
        File.WriteAllText(upgrade, System.Text.Json.JsonSerializer.Serialize(store.Library).Replace("\"SchemaVersion\":2", "\"SchemaVersion\":1"));
        var migrated = new LibraryStore(upgrade);
        Check(migrated.Library.SchemaVersion == 2 && migrated.Library.Entries.Count == store.Library.Entries.Count, "版本一收藏迁移");
        migrated.RecordLaunch(file, "测试");
        migrated.RecordLaunch(file, "测试");
        Check(new LibraryStore(upgrade).Library.Launches.Single().Count == 2, "自身启动次数持久化");
        migrated.Change(s => s.RecordLaunches = false);
        migrated.RecordLaunch(second, "不记录");
        Check(migrated.Library.Launches.Count == 1, "关闭记录生效");
        migrated.Change(s => s.RecordLaunches = true);
        for (var i = 0; i < 101; i++) migrated.RecordLaunch("fixture-" + i, "测试 " + i);
        Check(migrated.Library.Launches.Count == 100, "最近记录有界");
        migrated.DeleteCategory(category);
        Check(migrated.Library.Entries.All(e => e.CategoryId != category) && migrated.Library.Entries.Count == store.Library.Entries.Count, "删除分类保留收藏");
        migrated.Change(s => s.Launches.Clear());
        Check(migrated.Library.Entries.Count == store.Library.Entries.Count, "清除记录保留收藏");
        var gesture = new WinKeyGesture();
        Check(!gesture.Update(0x5B, true, false) && gesture.Update(0x5B, false, false), "单独 Win 键");
        gesture.Update(0x5B, true, false); gesture.Update(0x52, true, false);
        Check(!gesture.Update(0x5B, false, false), "Win 组合键保留");
        foreach (var action in new[] { SystemAction.Restart, SystemAction.ShutDown })
        {
            var arguments = SystemActions.Arguments(action);
            Check(!arguments.Contains("/f") && arguments[^1] == "0", "电源不强制结束应用");
        }

        var program = Path.Combine(fixture, "示例程序.exe");
        File.WriteAllText(program, "fixture only, never executed");
        store.AddPaths([program], Guid.Empty);
        var originalOrder = store.Library.Entries.ToArray();
        var appCandidates = Enumerable.Range(0, 40).Select(i => new SoftwareCandidate("软件" + i, "app-" + i, "exe-" + i)).ToList();
        var usage = Enumerable.Range(0, 40).ToDictionary(i => "exe-" + i, i => i + 1, StringComparer.OrdinalIgnoreCase);
        var appResults = MySoftware.Build([], appCandidates, [], usage, new HashSet<string>());
        Check(appResults.Count == 30 && appResults[0].Target == "app-39" && appResults[^1].Target == "app-10", "我的软件按使用活动降序且最多三十项");
        Check(MySoftware.Build([], appCandidates, [], new Dictionary<string, int>(), new HashSet<string>()).Count == 0, "无活动依据不填充已安装软件推荐");
        Check(MySoftware.Build([], appCandidates, [], usage, new HashSet<string> { "app-39" })[0].Target == "app-38", "我的软件隐藏项排除");
        var window = new MainWindow(store, false);
        var groupedList = (System.Windows.Controls.ListBox)window.FindName("EntryList");
        Check(groupedList.Items.OfType<MenuEntry>().Single().Target == program && store.Library.Entries.SequenceEqual(originalOrder), "我的软件仅显示程序，不改原入口顺序");
        var navigation = (System.Windows.Controls.ListBox)window.FindName("CategoryList");
        var priorLaunches = store.Library.Launches.ToList();
        store.Change(s =>
        {
            s.ShowWindowsRecentDocuments = false;
            s.Launches = [new(file, "文件", DateTimeOffset.UtcNow, 1), new(fixture, "目录", DateTimeOffset.UtcNow, 9), new(program, "程序", DateTimeOffset.UtcNow, 3)];
        });
        navigation.SelectedIndex = 1;
        Check(groupedList.Items.OfType<MenuEntry>().Single().Target == file, "最近打开界面仅显示文件，不显示目录和程序");
        navigation.SelectedItem = navigation.Items.OfType<MenuCategory>().Single(c => c.Name == "常用文件夹");
        Check(groupedList.Items.OfType<MenuEntry>().Single().Target == fixture, "常用文件夹界面过滤程序与文件并保留固定目录");
        var activitySort = (System.Windows.Controls.Button)window.FindName("ActivitySortButton");
        activitySort.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(store.Library.FoldersRecentFirst && activitySort.Content.ToString()!.Contains("最近打开"), "文件夹同页切换最近并保存");
        window.websiteHistory = WebsiteHistory.Read(CancellationToken.None, [Path.Combine(fixture, "Chrome History"), Path.Combine(fixture, "Edge History")]);
        navigation.SelectedItem = navigation.Items.OfType<MenuCategory>().Single(c => c.Name == "我的网站");
        Check(groupedList.Items.OfType<MenuEntry>().First().Target == "https://work.example/", "网站页面默认常用排序");
        activitySort.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(store.Library.WebsitesRecentFirst && groupedList.Items.OfType<MenuEntry>().First().Target == "https://recent.example/", "网站同页切换最近并保存");
        Check(navigation.Items.OfType<MenuCategory>().Select(c => c.Name).SequenceEqual(new[] { "我的软件", "最近打开", "常用文件夹", "我的网站", "我的电脑" }), "五个固定入口，无未分类和自定义分类");
        navigation.SelectedIndex = 0;
        store.Change(s => { s.ShowWindowsRecentDocuments = true; s.Launches = priorLaunches; });
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(712, 604));
        content.Arrange(new Rect(0, 0, 712, 604));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var context = backdrop.RenderOpen()) context.DrawRectangle(new SolidColorBrush(Color.FromRgb(250, 252, 255)), null, new Rect(0, 0, 740, 660));
        bitmap.Render(backdrop);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, "menu.png"))) encoder.Save(stream);
        ((System.Windows.Controls.Button)window.FindName("ViewToggle")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        content.Measure(new Size(712, 604));
        content.Arrange(new Rect(0, 0, 712, 604));
        content.UpdateLayout();
        Check(store.Library.IconView, "图标视图切换持久化");
        var icons = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        icons.Render(backdrop); icons.Render(content);
        var iconEncoder = new PngBitmapEncoder(); iconEncoder.Frames.Add(BitmapFrame.Create(icons));
        using (var stream = File.Create(Path.Combine(output, "menu-icons.png"))) iconEncoder.Save(stream);
        content.Measure(new Size(592, 484));
        content.Arrange(new Rect(0, 0, 592, 484));
        content.UpdateLayout();
        foreach (var name in new[] { "SearchBox", "ViewToggle", "ModeStatus", "EntryList" })
        {
            var control = (FrameworkElement)window.FindName(name);
            var bounds = control.TransformToAncestor(content).TransformBounds(new Rect(control.RenderSize));
            Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= 592 && bounds.Bottom <= 484,
                "紧凑尺寸控件边界：" + name);
        }
        var compact = new RenderTargetBitmap(620, 540, 96, 96, PixelFormats.Pbgra32);
        compact.Render(backdrop); compact.Render(content);
        var compactEncoder = new PngBitmapEncoder(); compactEncoder.Frames.Add(BitmapFrame.Create(compact));
        using (var stream = File.Create(Path.Combine(output, "menu-compact.png"))) compactEncoder.Save(stream);
        var search = (System.Windows.Controls.TextBox)window.FindName("SearchBox");
        window.candidates = Enumerable.Range(0, 40).Select(i => new SoftwareCandidate("应用" + i, "missing-" + i)).Append(new("Codex", "missing-codex")).ToList();
        search.Text = "codex";
        Check(groupedList.Items.Count == 1, "搜索软件忽略大小写");
        search.Text = "应用";
        Check(groupedList.Items.Count == 30, "我的软件搜索最多三十项");
        search.Text = "没有匹配项";
        Check(groupedList.Items.Count == 0, "软件无匹配结果");
        window.candidates = [new("ChatGPT", @"shell:AppsFolder\OpenAI.Codex_test!App")];
        search.Text = "Codex";
        Check(groupedList.Items.Count == 1, "商店应用标识搜索");
        search.Clear();
        Check(window.FindName("SoftwarePanel") is null && window.FindName("CategoryName") is null, "软件推荐和分类输入控件已移除");
        window.candidates = [];
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var actual = SoftwareDiscovery.Read(timeout.Token);
            File.WriteAllText(Path.Combine(output, "software-discovery.txt"), $"实际本机软件入口数量：{actual.Count}；Codex 标识匹配数量：{actual.Count(c => c.Shortcut.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase))}");
            var codex = actual.FirstOrDefault(c => c.Shortcut.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase));
            if (codex is not null)
            {
                var appIcon = new ShellIconConverter().Convert(codex.Shortcut, typeof(ImageSource), null!, System.Globalization.CultureInfo.CurrentCulture) as BitmapSource;
                Check(appIcon is not null && appIcon.IsFrozen, "本机 Codex 商店图标读取并冻结");
                var appIconEncoder = new PngBitmapEncoder();
                appIconEncoder.Frames.Add(BitmapFrame.Create(appIcon!));
                using var iconStream = File.Create(Path.Combine(output, "codex-icon.png"));
                appIconEncoder.Save(iconStream);
            }
        }
        var inputSurface = (System.Windows.Controls.Border)search.Template.FindName("Surface", search);
        Check(inputSurface.CornerRadius.TopLeft == 14, "旧配置默认 Google 风格");
        var originalEntries = store.Library.Entries.ToArray();
        store.Change(s => s.CustomMenuEnabled = true);
        window.SetTheme("Windows");
        foreach (var nav in new[] { new MenuCategory(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), "全部收藏"), new MenuCategory(new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "最近打开"), new MenuCategory(Guid.Empty, "常用文件夹"), new MenuCategory(Guid.NewGuid(), "常用软件") })
        {
            var icon = new CategoryIconConverter().Convert(nav, typeof(ImageSource), null!, System.Globalization.CultureInfo.CurrentCulture) as BitmapSource;
            Check(icon is not null && icon.IsFrozen && icon.PixelWidth == 36 && icon.PixelHeight == 36, "Fluent 导航图标嵌入与尺寸");
        }
        Check(new LibraryStore(Path.Combine(store.DirectoryPath, "library.json")).Library.Theme == "Windows", "Windows 风格保存重开");
        ((System.Windows.Controls.Button)window.FindName("ViewToggle")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        content.Measure(new Size(712, 604));
        content.Arrange(new Rect(0, 0, 712, 604));
        content.UpdateLayout();
        Check(inputSurface.CornerRadius.TopLeft == 6, "动态更新输入框形状");
        var windowsBitmap = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        windowsBitmap.Render(backdrop); windowsBitmap.Render(content);
        var windowsEncoder = new PngBitmapEncoder(); windowsEncoder.Frames.Add(BitmapFrame.Create(windowsBitmap));
        using (var stream = File.Create(Path.Combine(output, "menu-windows.png"))) windowsEncoder.Save(stream);
        navigation.SelectedItem = navigation.Items.OfType<MenuCategory>().Single(c => c.Name == "我的网站");
        content.UpdateLayout();
        var sitesBitmap = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        sitesBitmap.Render(backdrop); sitesBitmap.Render(content);
        var sitesEncoder = new PngBitmapEncoder(); sitesEncoder.Frames.Add(BitmapFrame.Create(sitesBitmap));
        using (var stream = File.Create(Path.Combine(output, "menu-websites.png"))) sitesEncoder.Save(stream);
        window.computer = new([new("系统 (C:)", "C:\\", "可用 120 GB / 共 256 GB", 53), new("资料 (D:)", "D:\\", "可用 640 GB / 共 1 TB", 36), new("资料 (E:)", "E:\\", "可用 527 GB / 共 621 GB", 15), new("资料 (F:)", "F:\\", "可用 606 GB / 共 621 GB", 3), new("G:\\", "G:\\", "设备未就绪")],
            new[] { "系统和安全", "网络和 Internet", "硬件和声音", "程序", "用户账户", "外观和个性化", "时钟和区域" }.Select((title, index) => new ComputerItem(title, ComputerItems.ControlPanel + "\\" + new[] { 5, 3, 2, 8, 9, 1, 6 }[index])).ToList(), false);
        navigation.SelectedItem = navigation.Items.OfType<MenuCategory>().Single(c => c.Name == "我的电脑");
        content.UpdateLayout();
        Check(groupedList.Visibility == Visibility.Collapsed && ((System.Windows.Controls.ScrollViewer)window.FindName("ComputerScroll")).Visibility == Visibility.Visible, "我的电脑独立内容区切换");
        var computerScroll = (System.Windows.Controls.ScrollViewer)window.FindName("ComputerScroll");
        var computerBitmap = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        computerBitmap.Render(backdrop); computerBitmap.Render(content);
        var computerEncoder = new PngBitmapEncoder(); computerEncoder.Frames.Add(BitmapFrame.Create(computerBitmap));
        using (var stream = File.Create(Path.Combine(output, "menu-computer.png"))) computerEncoder.Save(stream);
        Check(computerScroll.ExtentHeight <= computerScroll.ViewportHeight + 1, $"默认尺寸下五个磁盘与七个控制面板分类无需滚动：内容 {computerScroll.ExtentHeight}，可视 {computerScroll.ViewportHeight}");
        search.Text = "网络";
        Check(((System.Windows.Controls.StackPanel)window.FindName("ComputerPanel")).Children.OfType<System.Windows.Controls.Primitives.UniformGrid>().SelectMany(g => g.Children.OfType<System.Windows.Controls.Button>()).Count() == 1, "我的电脑搜索过滤系统入口");
        search.Clear();
        navigation.SelectedIndex = 0;
        window.SetTheme("Google");
        content.UpdateLayout();
        Check(inputSurface.CornerRadius.TopLeft == 14, "往返切换即时生效");
        Check(new LibraryStore(Path.Combine(store.DirectoryPath, "library.json")).Library.Theme == "Google"
            && store.Library.Entries.SequenceEqual(originalEntries) && store.Library.CustomMenuEnabled,
            "往返主题保存且收藏顺序与自定义接入不变");
        var futureTheme = Path.Combine(output, "future-theme.json");
        File.WriteAllText(futureTheme, "{\"SchemaVersion\":2,\"Theme\":\"future\"}");
        Check(new LibraryStore(futureTheme).Library.Theme == "Google", "未知主题回退默认风格");
        // 生命周期检查不启用系统接入，也不发送键盘或鼠标输入。
        store.Change(s => { s.CustomMenuEnabled = false; });
        var resident = new MainWindow(store);
        var closed = false;
        resident.Closed += (_, _) => closed = true;
        resident.Show();
        resident.WindowState = WindowState.Minimized;
        resident.ShowMenu(null);
        Check(resident.IsVisible && resident.WindowState == WindowState.Normal, "显示入口恢复最小化窗口");
        Check(resident.Topmost, "未接入时通过图标打开也保留菜单浮层");
        resident.Topmost = true;
        resident.Close();
        Check(!closed && !resident.IsVisible && !resident.Topmost, "关闭窗口收起到托盘并解除浮层");
        resident.RequestExit("smoke");
        Check(closed, "显式退出结束窗口");
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 截屏选区移动缩放、彩色标注、画笔文字马赛克及 PNG 导出；Win10/11 试用版本策略、传统 Start 窗口定位与隐藏/越界过滤；更新版本选择、下载完整性、取消和失败清理；我的电脑视图及搜索； 添加、去重、分类排序、持久化、源文件保留、配置恢复、未知版本保护、写入失败回退、软件发现过滤、推荐偏好、schema 迁移、最近记录次数/关闭/上限、删除分类保留收藏、Win 组合键模型、电源参数、WPF 默认/图标/紧凑布局及关键控件边界、Windows/Google 即时往返切换、主题保存重开与未知值回退。\n真实鼠标拖放仍待人工验收；开始入口实机验证另见 input-result.txt。");
        window.Close();
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
    }
    private static void VerifySoftwareDiscovery(string fixture)
    {
        var folder = Path.Combine(fixture, "shortcuts");
        Directory.CreateDirectory(folder);
        foreach (var name in new[] { "示例软件", "重复软件", "卸载工具", "失效软件", "文档" })
        {
            var target = name == "失效软件" ? Path.Combine(fixture, "missing.exe")
                : name == "文档" ? Path.Combine(fixture, "中文 文档.txt") : Environment.ProcessPath!;
            ShellShortcut.CreateFixture(Path.Combine(folder, name + ".lnk"), target);
        }
        var result = SoftwareDiscovery.Read(CancellationToken.None, [folder]);
        Check(result.Count == 1 && File.Exists(result[0].Shortcut), "软件发现过滤与按目标去重");
        var shortcut = Path.Combine(folder, "示例软件.lnk");
        var targetBefore = ShellShortcut.ReadTarget(shortcut);
        var shortcutIcon = new ShellIconConverter().Convert(shortcut, typeof(ImageSource), null!, System.Globalization.CultureInfo.InvariantCulture);
        Check(shortcutIcon is ImageSource && ShellShortcut.ReadTarget(shortcut) == targetBefore,
            "快捷方式读取原始图标且不改变启动目标");
    }
}
