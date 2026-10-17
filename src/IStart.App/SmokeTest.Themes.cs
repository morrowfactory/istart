using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace IStart;

internal static partial class SmokeTest
{
    private static void VerifyCompactThemes(string output)
    {
        var folder = Path.Combine(output, "theme-fixture");
        Directory.CreateDirectory(folder);
        var store = new LibraryStore(Path.Combine(folder, "library.json"));
        store.Change(s => { s.Theme = "Windows"; s.Language = "zh-CN"; s.IconView = true; s.ShowWindowsRecentDocuments = false; });
        var apps = Enumerable.Range(1, 12).Select(i => Path.Combine(folder, $"App {i:00}.exe")).ToArray();
        foreach (var app in apps) File.WriteAllText(app, "test fixture; never executed");
        store.AddPaths(apps, Guid.Empty);
        var window = new MainWindow(store, false);
        var content = (FrameworkElement)window.Content;
        var navigation = (ListBox)window.FindName("CategoryList");
        var search = (TextBox)window.FindName("SearchBox");
        var list = (ListBox)window.FindName("EntryList");
        foreach (var theme in new[] { "Windows", "Google" })
        {
            window.SetTheme(theme);
            var columns = theme == "Windows" ? 6 : 5;
            RenderLanguage(content, 724, theme == "Windows" ? 449 : 529, Path.Combine(output, "theme-" + theme + ".png"));
            Check(Grid.GetRow(navigation) == (theme == "Windows" ? 1 : 2), "双主题导航布局区分：" + theme);
            Check(list.Items.Count == 12 && list.ItemContainerGenerator.ContainerFromIndex(columns) is FrameworkElement, "十二个动态软件入口均可布局");
            var first = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var last = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(columns - 1);
            var next = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(columns);
            var p0 = first.TranslatePoint(new Point(), list);
            var pLast = last.TranslatePoint(new Point(), list);
            var pNext = next.TranslatePoint(new Point(), list);
            Check(Math.Abs(pLast.Y - p0.Y) < 1 && pNext.Y > p0.Y + 60 && Math.Abs(pNext.X - p0.X) < 1,
                "Windows 六列 / Google 五列：" + theme);
            foreach (var name in new[] { "ScreenshotButton", "SettingsButton", "ViewToggle", "AddFileButton", "AccountButton", "NativeMenuButton", "PowerButton", "ExitButton" })
            {
                var button = (Button)window.FindName(name);
                Check(!string.IsNullOrWhiteSpace(button.ToolTip?.ToString()) && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)), "图标操作有说明与无障碍名称：" + name);
                Check(name == "SettingsButton" || button.Content is BitmapSource, "操作显示真实本地图标：" + name);
                var bounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
                Check(bounds.Left >= 0 && bounds.Right <= 725 && bounds.Bottom <= content.ActualHeight + 1, "图标操作在可视范围：" + theme + "/" + name);
            }
        }
        search.Text = "App 01";
        window.SetTheme("Windows");
        Check(ReferenceEquals(search, window.FindName("SearchBox")) && ReferenceEquals(navigation, window.FindName("CategoryList"))
            && search.Text == "App 01" && list.Items.Count == 1, "主题切换保留控件、页面、搜索和动态结果");
        window.Close();
        File.WriteAllText(Path.Combine(output, "theme-result.txt"), "PASS：双主题独立导航与六/五列网格；本地图标、悬停说明和无障碍名称；默认尺寸控件边界；主题切换保留搜索状态。渲染使用十二个测试软件入口，未执行。\n");
    }
}
