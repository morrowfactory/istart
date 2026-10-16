using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace IStart;

internal static partial class SmokeTest
{
    private static void VerifyLocalization(string output)
    {
        Check(L.Resolve("System", CultureInfo.GetCultureInfo("zh-CN")) == "zh-CN"
            && L.Resolve("System", CultureInfo.GetCultureInfo("en-GB")) == "en"
            && L.Resolve("System", CultureInfo.GetCultureInfo("de-DE")) == "en", "系统语言与未支持语言的回退");
        Check(L.Resolve("en", CultureInfo.GetCultureInfo("zh-CN")) == "en"
            && L.Resolve("zh-CN", CultureInfo.GetCultureInfo("en-US")) == "zh-CN", "手动语言优先于系统");
        foreach (var (source, english) in L.English)
        {
            Check(!string.IsNullOrWhiteSpace(english), "英文翻译不能为空：" + source);
            Check(CompositeFormat.Parse(source).MinimumArgumentCount == CompositeFormat.Parse(english).MinimumArgumentCount, "翻译格式占位符一致：" + source);
        }
        var folder = Path.Combine(output, "language-fixture");
        Directory.CreateDirectory(folder);
        var config = Path.Combine(folder, "library.json");
        File.WriteAllText(config, "{\"SchemaVersion\":2}");
        var store = new LibraryStore(config);
        Check(store.Library.Language == "System", "旧配置默认跟随系统");
        File.WriteAllText(config, "{\"SchemaVersion\":2,\"Language\":\"future\"}");
        Check(new LibraryStore(config).Library.Language == "System", "未知语言回退跟随系统");
        var app = Path.Combine(folder, "中文软件.exe");
        File.WriteAllText(app, "fixture");
        store.AddPaths([app], Guid.Empty);
        store.Change(s => { s.Language = "zh-CN"; s.Theme = "Windows"; s.CustomMenuEnabled = true; s.RecordLaunches = false; s.ScreenshotShortcut = "关闭"; });
        var entries = store.Library.Entries.ToArray();
        var window = new MainWindow(store, false);
        var navigation = (ListBox)window.FindName("CategoryList");
        var search = (TextBox)window.FindName("SearchBox");
        navigation.SelectedIndex = 3;
        search.Text = "example";
        var selected = ((MenuCategory)navigation.SelectedItem).Id;
        var settings = window.CreateSettings();
        var panel = (StackPanel)((ScrollViewer)settings.Content).Content;
        var language = panel.Children.OfType<ComboBox>().Single(c => c.Name == "LanguageChoice");
        var options = panel.Children.OfType<CheckBox>().ToArray();
        options[1].IsChecked = true; // 切换语言不能重建并丢失尚未保存的选项。
        language.SelectedValue = "en";
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Check(store.Library.Language == "en" && new LibraryStore(config).Library.Language == "en", "英文设置保存重开");
        var reopened = new MainWindow(new LibraryStore(config), false);
        Check(reopened.Title == "iStart · Personal Start Menu", "重开窗口应用已保存的英文语言");
        reopened.Close();
        Check(settings.Title == "iStart Settings" && panel.Children.OfType<TextBlock>().Any(t => t.Text == "Menu and privacy"), "设置窗口即时切换英文");
        Check(options[1].IsChecked == true && store.Library.StartWithWindows == false, "语言切换保留未保存选项，不修改开机启动");
        Check(navigation.Items.OfType<MenuCategory>().Select(c => c.Name).SequenceEqual(new[] { "My Apps", "Recent Files", "Folders", "My Websites", "My Computer" }), "五个英文导航");
        Check(((MenuCategory)navigation.SelectedItem).Id == selected && search.Text == "example", "语言切换保留当前页面和搜索词");
        Check(window.Title == "iStart · Personal Start Menu" && ((Button)window.FindName("ScreenshotButton")).Content.ToString() == "Capture", "主窗口 XAML 文字即时切换");
        Check(ComputerItems.CategoryName(ComputerItems.ControlPanel + "\\5") == "System and Security", "系统分类按 ID 显示英文");
        var shortcut = panel.Children.OfType<ComboBox>().Single(c => c.Items.OfType<ComboBoxItem>().Any(i => Equals(i.Tag, "Alt+Shift+A")));
        Check(shortcut.SelectedValue!.ToString() == "关闭" && ((ComboBoxItem)shortcut.SelectedItem).Content.ToString() == "Off", "关闭快捷键只翻译显示文字");
        Check(store.Library.Entries.SequenceEqual(entries) && store.Library.CustomMenuEnabled && !store.Library.RecordLaunches && store.Library.Theme == "Windows", "切换语言保留数据、隐私、主题和接入偏好");

        navigation.SelectedIndex = 0;
        search.Clear();
        foreach (var theme in new[] { "Windows", "Google" })
        {
            window.SetTheme(theme);
            RenderLanguage(window.Content as FrameworkElement ?? throw new InvalidOperationException(), 712, 604, Path.Combine(output, "menu-en-" + theme + ".png"));
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(592, 484)); content.Arrange(new Rect(0, 0, 592, 484)); content.UpdateLayout();
            foreach (var name in new[] { "SearchBox", "ScreenshotButton", "ModeStatus", "EnableMenuButton", "EntryList" })
            {
                var element = (FrameworkElement)window.FindName(name);
                var bounds = element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
                Check(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= 593 && bounds.Bottom <= 485, "英文紧凑布局边界：" + theme + "/" + name);
            }
            RenderLanguage(content, 592, 484, Path.Combine(output, "menu-en-compact-" + theme + ".png"));
            foreach (var page in new[] { 1, 2, 3, 4 })
            {
                navigation.SelectedIndex = page;
                RenderLanguage(content, 592, 484, Path.Combine(output, $"menu-en-compact-{theme}-{page}.png"));
                var title = (TextBlock)window.FindName("SectionTitle");
                foreach (var control in new[] { "ActivitySortButton", "ViewToggle", "AddFileButton", "AddFolderButton" })
                {
                    var button = (Button)window.FindName(control);
                    if (button.Visibility != Visibility.Visible) continue;
                    var titleBounds = title.TransformToAncestor(content).TransformBounds(new Rect(title.RenderSize));
                    var buttonBounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
                    Check(titleBounds.Right <= buttonBounds.Left + 1, "英文标题与操作按钮不重叠：" + theme + "/" + page);
                }
            }
            navigation.SelectedIndex = 0;
        }
        RenderLanguage((FrameworkElement)settings.Content, 454, 930, Path.Combine(output, "settings-en.png"));
        var pixels = Enumerable.Repeat((byte)255, 800 * 600 * 4).ToArray();
        var frame = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgra32, null, pixels, 800 * 4);
        frame.Freeze();
        var capture = new ScreenshotWindow(frame, new System.Drawing.Rectangle(0, 0, 800, 600));
        capture.Surface.BeginDrag(new Point(80, 80)); capture.Surface.DragTo(new Point(650, 400)); capture.Surface.EndDrag();
        Check(capture.Title == "iStart Screenshot · Esc to cancel" && capture.ResultMessage == "Screenshot cancelled.", "截屏窗口与结果使用英文");
        RenderLanguage((FrameworkElement)capture.Content, 800, 600, Path.Combine(output, "screenshot-en.png"));
        capture.SetTool(CaptureTool.Arrow);
        RenderLanguage((FrameworkElement)capture.Content, 800, 600, Path.Combine(output, "screenshot-en-arrow.png"));
        capture.Close();
        language.SelectedValue = "zh-CN";
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Check(window.Title == "iStart · 个人开始菜单" && settings.Title == "iStart 设置", "中英文往返切换即时更新");
        language.SelectedValue = "System";
        Check(store.Library.Language == "System" && new LibraryStore(config).Library.Language == "System", "手动语言可恢复跟随系统并保存");
        settings.Close();
        window.Close();
        L.Apply("zh-CN");
        File.WriteAllText(Path.Combine(output, "language-result.txt"), "PASS：默认系统语言、中英文及未支持语言回退；旧配置兼容和语言持久化；设置与主菜单即时往返切换；导航/搜索/用户数据/隐私/主题/接入偏好保留；快捷键稳定标识；英文双主题默认与紧凑布局。未改变宿主显示语言、开机启动或开始菜单接入。\n");
    }

    private static void RenderLanguage(FrameworkElement content, double width, double height, string path)
    {
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var image = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var drawing = backdrop.RenderOpen()) drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        image.Render(backdrop);
        image.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
