using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

internal static class SmokeTest
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
        store.Change(s => { s.RecommendationsEnabled = false; s.ExcludedRecommendations.Add("excluded-shortcut"); });
        var preferences = new LibraryStore(config);
        Check(!preferences.Library.RecommendationsEnabled && preferences.Library.ExcludedRecommendations.Contains("excluded-shortcut"), "推荐偏好持久化");

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

        var window = new MainWindow(store, false);
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
        window.candidates = Enumerable.Range(0, 20).Select(i => new SoftwareCandidate("应用" + i, "missing-" + i)).Append(new("Codex", "missing-codex")).ToList();
        search.Text = "codex";
        var softwareResults = (System.Windows.Controls.WrapPanel)window.FindName("Recommendations");
        Check(softwareResults.Children.Count == 1, "搜索全部软件而非推荐前十二项，忽略大小写");
        search.Text = "应用";
        Check(softwareResults.Children.Count == 20, "软件搜索不受推荐数量限制");
        search.Text = "没有匹配项";
        Check(softwareResults.Children.Count == 0 && ((System.Windows.Controls.TextBlock)window.FindName("SearchStatus")).Text.Length > 0, "无结果提示");
        store.Change(s => s.RecommendationsEnabled = false);
        search.Text = "codex";
        Check(softwareResults.Children.Count == 1, "关闭推荐仍可搜索软件");
        window.candidates = [new("ChatGPT", "shell:AppsFolder\\OpenAI.Codex_test!App")];
        search.Text = "Codex";
        Check(softwareResults.Children.Count == 1, "商店显示名不同仍可按应用标识搜索");
        store.Change(s => s.RecommendationsEnabled = true);
        search.Clear();
        window.candidates = [];
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var actual = SoftwareDiscovery.Read(timeout.Token);
            File.WriteAllText(Path.Combine(output, "software-discovery.txt"), $"实际本机软件入口数量：{actual.Count}；Codex 标识匹配数量：{actual.Count(c => c.Shortcut.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase))}");
        }
        var inputSurface = (System.Windows.Controls.Border)search.Template.FindName("Surface", search);
        Check(inputSurface.CornerRadius.TopLeft == 14, "旧配置默认 Google 风格");
        var originalEntries = store.Library.Entries.ToArray();
        store.Change(s => s.CustomMenuEnabled = true);
        window.SetTheme("Windows");
        Check(new LibraryStore(Path.Combine(store.DirectoryPath, "library.json")).Library.Theme == "Windows", "Windows 风格保存重开");
        ((System.Windows.Controls.Button)window.FindName("ViewToggle")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        content.Measure(new Size(712, 604));
        content.Arrange(new Rect(0, 0, 712, 604));
        content.UpdateLayout();
        Check(inputSurface.CornerRadius.TopLeft == 4, "动态更新输入框形状");
        var windowsBitmap = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        windowsBitmap.Render(backdrop); windowsBitmap.Render(content);
        var windowsEncoder = new PngBitmapEncoder(); windowsEncoder.Frames.Add(BitmapFrame.Create(windowsBitmap));
        using (var stream = File.Create(Path.Combine(output, "menu-windows.png"))) windowsEncoder.Save(stream);
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
        store.Change(s => { s.CustomMenuEnabled = false; s.RecommendationsEnabled = false; });
        var resident = new MainWindow(store);
        var closed = false;
        resident.Closed += (_, _) => closed = true;
        resident.Show();
        resident.WindowState = WindowState.Minimized;
        resident.ShowMenu(null);
        Check(resident.IsVisible && resident.WindowState == WindowState.Normal, "显示入口恢复最小化窗口");
        Check(!resident.Topmost, "未接入时不保留浮层");
        resident.Topmost = true;
        resident.Close();
        Check(!closed && !resident.IsVisible && !resident.Topmost, "关闭窗口收起到托盘并解除浮层");
        resident.RequestExit("smoke");
        Check(closed, "显式退出结束窗口");
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 添加、去重、分类排序、持久化、源文件保留、配置恢复、未知版本保护、写入失败回退、软件发现过滤、推荐偏好、schema 迁移、最近记录次数/关闭/上限、删除分类保留收藏、Win 组合键模型、电源参数、WPF 默认/图标/紧凑布局及关键控件边界、Windows/Google 即时往返切换、主题保存重开与未知值回退。\n真实鼠标拖放仍待人工验收；开始入口实机验证另见 input-result.txt。");
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
