using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace IStart;

internal static partial class ReliabilityTest
{
    // Run only after RunWindows has verified the dedicated inactive desktop.
    private static async Task VerifyCachedOpening(string output)
    {
        var store = new LibraryStore(Path.Combine(output, "cached-menu", "library.json"));
        store.Change(s => { s.Language = "en"; s.AutomaticUpdates = false; });
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var failRead = false;
        MainWindow? window = null;
        window = new MainWindow(store, false, null, async () =>
        {
            reads++;
            entered.TrySetResult();
            await release.Task;
            if (failRead) throw new IOException("fixture unreadable source");
            window!.candidates = [new("cached-fresh", "fixture-fresh")];
            window.softwareUsage = new() { ["fixture-fresh"] = 99 };
        });
        var samples = new List<double>();
        try
        {
            var list = (ListBox)window.FindName("EntryList");
            var search = (TextBox)window.FindName("SearchBox");
            var navigation = (ListBox)window.FindName("CategoryList");
            window.candidates = Enumerable.Range(0, 30).Select(i => new SoftwareCandidate("cached-" + i, "fixture-" + i)).ToList();
            window.softwareUsage = Enumerable.Range(0, 30).ToDictionary(i => "fixture-" + i, i => i + 1);
            search.Text = "cached-";
            list.SelectedIndex = 20;
            var cached = list.ItemsSource;
            var selected = list.SelectedItem;
            window.ShowMenu(null);
            Check(reads == 0 && ReferenceEquals(cached, list.ItemsSource), "first Show must retain cached rows without starting activity work");
            window.Hide();
            await Task.Delay(300);
            Check(reads == 0, "hiding before the deferred refresh must cancel the pending read");

            window.ShowMenu(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var heartbeat = false;
            await window.Dispatcher.InvokeAsync(() => heartbeat = true, DispatcherPriority.Input);
            Check(heartbeat && !release.Task.IsCompleted && reads == 1, "blocked activity refresh must leave UI input dispatch responsive");
            var scroll = FindScroll(list);
            scroll?.ScrollToVerticalOffset(180);
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            var offset = scroll?.VerticalOffset;
            foreach (var theme in new[] { "Windows", "Google" })
            {
                window.SetTheme(theme);
                cached = list.ItemsSource;
                selected = list.SelectedItem;
                for (var i = 0; i < 10; i++)
                {
                    window.Hide();
                    var watch = Stopwatch.StartNew();
                    window.ShowMenu(null);
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                    Check(window.IsVisible && ReferenceEquals(cached, list.ItemsSource)
                        && ReferenceEquals(selected, list.SelectedItem) && search.Text == "cached-" && navigation.SelectedIndex == 0,
                        "reopening during a blocked refresh must retain rows, selection, query and page");
                    await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                    Check(reads == 1, "repeated opens must not launch overlapping activity reads");
                }
            }
            Check(offset is null || FindScroll(list)?.VerticalOffset == offset, "cached reopens must retain scroll position");
            release.TrySetResult();
            await Until(() => list.Items.Count == 1 && ((MenuEntry)list.Items[0]).Title == "cached-fresh");
            Check(search.Text == "cached-", "fresh results must not clear the retained search");
            cached = list.ItemsSource;
            list.SelectedIndex = 0;
            selected = list.SelectedItem;
            await window.RefreshVisibleContent();
            Check(ReferenceEquals(cached, list.ItemsSource) && ReferenceEquals(selected, list.SelectedItem),
                "unchanged suggestions with fresh generated IDs must reuse list controls and selection");
            failRead = true;
            window.ShowMenu(null);
            await window.RefreshVisibleContent();
            Check(ReferenceEquals(cached, list.ItemsSource) && list.SelectedItem == selected,
                "a failed refresh must preserve usable cached rows and selection");
            failRead = false;

            // A newly pinned folder must get its real ID, even when its target/title are unchanged.
            var folder = Path.Combine(output, "cached-menu", "folder");
            Directory.CreateDirectory(folder);
            store.Change(s => s.Launches = [new(folder, "folder", DateTimeOffset.UtcNow, 3)]);
            search.Clear();
            navigation.SelectedIndex = 2;
            var suggestion = list.Items.OfType<MenuEntry>().Single();
            store.AddPaths([folder], Guid.Empty);
            var pin = store.Library.Entries.Single();
            await window.RefreshVisibleContent();
            Check(suggestion.Id != pin.Id && list.Items.OfType<MenuEntry>().Single().Id == pin.Id,
                "stable-row reuse must not retain a suggestion ID after pinning");

            window.computer = new([new("fixture disk", "C:\\", "fixture capacity", 20)], [], false);
            navigation.SelectedIndex = 4;
            var panel = (StackPanel)window.FindName("ComputerPanel");
            var card = panel.Children[1];
            var expanded = panel.Children.OfType<Expander>().Single();
            expanded.IsExpanded = false;
            window.computer = new([new("fixture disk", "C:\\", "fixture capacity", 20)], [], false);
            await window.RefreshVisibleContent();
            Check(ReferenceEquals(card, panel.Children[1]) && !expanded.IsExpanded,
                "unchanged computer results must preserve cards and expansion state");

            release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            navigation.SelectedIndex = 0;
            search.Text = "cached-";
            window.ShowMenu(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            search.Text = "unmatched-new-query";
            release.TrySetResult();
            await Task.Delay(400);
            Check(search.Text == "unmatched-new-query" && list.Items.Count == 0,
                "late activity results must not replace a newer search result");

            window.Hide();
            await Task.Delay(300);
            var afterHide = reads;
            await Task.Delay(250);
            Check(reads == afterHide, "hidden menus must not start another scheduled refresh");
            File.AppendAllText(Path.Combine(output, "window-result.txt"),
                $"通过：首帧使用缓存、收起取消待刷新、慢读取期间界面可响应、Windows/Google 共 20 次缓存弹出、读取合并、搜索/选中/滚动保留、新结果更新、失败保留、固定项真实 ID、电脑卡片复用及过期搜索结果拦截。\n" +
                $"慢刷新期间缓存 ShowMenu 调用：中位数 {samples.Order().ElementAt(samples.Count / 2):F1} 毫秒，最大 {samples.Max():F1} 毫秒（非活动隔离桌面，不代表实际 Win 键全链路延迟）。\n");
        }
        finally { release.TrySetResult(); window.Close(); }
    }

    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            Check(watch.Elapsed < TimeSpan.FromSeconds(4), "deferred fresh results must eventually replace the cached rows");
            await Task.Delay(25);
        }
    }

    private static ScrollViewer? FindScroll(DependencyObject parent)
    {
        if (parent is ScrollViewer scroll) return scroll;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindScroll(VisualTreeHelper.GetChild(parent, i)) is { } found) return found;
        return null;
    }
}
