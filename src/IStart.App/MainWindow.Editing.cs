using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace IStart;

public partial class MainWindow
{
    private MenuEntry? removedEntry;
    private int removedIndex;
    private DispatcherTimer? undoTimer;
    private DataTemplate? listTemplate;
    private ItemsPanelTemplate? listPanel;
    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "添加文件夹入口", Multiselect = true };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FolderNames, Guid.Empty, null);
    }
    private void ToggleView(object sender, RoutedEventArgs e)
    {
        Perform(() => store.Change(s => s.IconView = !s.IconView), "显示方式已保存；列表继续按当前使用活动排序。");
        ApplyView();
    }
    private void ApplyView()
    {
        listTemplate ??= EntryList.ItemTemplate;
        listPanel ??= EntryList.ItemsPanel;
        EntryList.ItemTemplate = store.Library.IconView && !recentView && !websiteView ? (DataTemplate)Resources["EntryTile"] : listTemplate;
        var panel = store.Library.IconView && !recentView && !websiteView ? (ItemsPanelTemplate)Resources["IconPanel"] : listPanel;
        EntryList.ItemsPanel = panel;
        ViewToggle.Content = store.Library.IconView ? "列表" : "图标";
    }
    private void RemoveById(Guid id)
    {
        if (websiteView)
        {
            var site = EntryList.Items.OfType<MenuEntry>().FirstOrDefault(entry => entry.Id == id);
            if (site is not null) { hiddenWebsites.Add(site.Target); RefreshEntries(); StatusText.Text = "已隐藏本次网站显示，浏览器历史保持不变。"; }
            return;
        }
        if (softwareView)
        {
            var app = EntryList.Items.OfType<MenuEntry>().FirstOrDefault(entry => entry.Id == id);
            if (app is not null) Perform(() => store.Change(s =>
            {
                if (!s.HiddenSoftware.Contains(app.Target, StringComparer.OrdinalIgnoreCase) && s.HiddenSoftware.Count < 256) s.HiddenSoftware.Add(app.Target);
            }), "已从我的软件中隐藏；可在设置中重置隐藏项。软件未卸载。");
            return;
        }
        if (recentView)
        {
            var item = EntryList.Items.OfType<MenuEntry>().FirstOrDefault(entry => entry.Id == id);
            if (item is not null) Perform(() => store.Change(s => s.Launches.RemoveAll(l => l.Target == item.Target)), "已移除此条最近记录，原文件保持不变。");
            return;
        }
        var entry = store.Library.Entries.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            var suggestion = EntryList.Items.OfType<MenuEntry>().FirstOrDefault(item => item.Id == id);
            if (folderView && suggestion is not null)
            {
                hiddenFolders.Add(Path.TrimEndingDirectorySeparator(suggestion.Target));
                RefreshEntries();
                StatusText.Text = "已隐藏本次文件夹建议；重新启动后可再次显示。原文件夹保持不变。";
            }
            return;
        }
        var index = store.Library.Entries.IndexOf(entry);
        Perform(() =>
        {
            store.Change(s => s.Entries.RemoveAll(item => item.Id == id));
            removedEntry = entry;
            removedIndex = index;
            UndoButton.Visibility = Visibility.Visible;
            undoTimer?.Stop();
            undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            undoTimer.Tick += (_, _) => { removedEntry = null; UndoButton.Visibility = Visibility.Collapsed; undoTimer.Stop(); };
            undoTimer.Start();
        }, "已移除入口，可在 8 秒内撤销；原文件保持不变。");
    }
    private void UndoRemove(object sender, RoutedEventArgs e)
    {
        if (removedEntry is not MenuEntry entry) return;
        Perform(() => store.Change(s =>
        {
            if (s.Entries.Any(item => item.Target.Equals(entry.Target, StringComparison.OrdinalIgnoreCase))) return;
            if (!s.Categories.Any(c => c.Id == entry.CategoryId)) entry = entry with { CategoryId = Guid.Empty };
            s.Entries.Insert(Math.Min(removedIndex, s.Entries.Count), entry);
        }), "入口已恢复。");
        removedEntry = null;
        undoTimer?.Stop();
        UndoButton.Visibility = Visibility.Collapsed;
    }
    private void EntryKeyDown(object sender, KeyEventArgs e)
    {
        if (EntryList.SelectedItem is not MenuEntry entry) return;
        if (e.Key == Key.Enter) { OpenTarget(entry.Target, entry.Title); e.Handled = true; }
        else if (e.Key == Key.Delete) { RemoveById(entry.Id); e.Handled = true; }
    }
    private void EntryContextMenu(object sender, ContextMenuEventArgs e)
    {
        var entry = ItemAt<MenuEntry>(EntryList, e.OriginalSource) ?? EntryList.SelectedItem as MenuEntry;
        if (entry is null) { e.Handled = true; return; }
        EntryList.SelectedItem = entry;
        var menu = new ContextMenu();
        MenuItem Action(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }
        Action("打开", () => OpenTarget(entry.Target, entry.Title));
        if (websiteView)
        {
            Action("隐藏本次显示", () => RemoveById(entry.Id));
            EntryList.ContextMenu = menu;
            return;
        }
        if (softwareView)
        {
            Action("从我的软件中隐藏", () => RemoveById(entry.Id));
            EntryList.ContextMenu = menu;
            return;
        }
        if (folderView && !store.Library.Entries.Any(item => item.Id == entry.Id))
        {
            Action("固定到常用文件夹", () => Perform(() =>
            {
                var existing = store.Library.Entries.FirstOrDefault(item => item.Target.Equals(entry.Target, StringComparison.OrdinalIgnoreCase));
                if (existing is null) store.AddPaths([entry.Target], Guid.Empty);
                else store.Move(existing.Id, Guid.Empty, null);
            }, "文件夹已固定，后续自动补充不会改变其顺序。"));
            Action("隐藏本次建议", () => RemoveById(entry.Id));
            EntryList.ContextMenu = menu;
            return;
        }
        if (!recentView)
        {
            void Relocate(string target) => Perform(() => store.Change(s =>
            {
                if (s.Entries.Any(item => item.Id != entry.Id && item.Target.Equals(target, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("目标已收藏");
                s.Entries[s.Entries.FindIndex(item => item.Id == entry.Id)] = entry with { Target = target };
            }), "入口目标已更新。");
            Action("重命名入口", () =>
            {
                var title = Prompt("入口名称", entry.Title);
                if (!string.IsNullOrWhiteSpace(title)) Perform(() => store.Change(s => s.Entries[s.Entries.FindIndex(item => item.Id == entry.Id)] = entry with { Title = title }), "名称已保存。");
            });
            Action("重新定位文件…", () =>
            {
                var dialog = new OpenFileDialog { Title = "选择新的目标文件或快捷方式" };
                if (dialog.ShowDialog(this) == true) Relocate(dialog.FileName);
            });
            Action("重新定位文件夹…", () =>
            {
                var dialog = new OpenFolderDialog { Title = "选择新的目标文件夹" };
                if (dialog.ShowDialog(this) == true) Relocate(dialog.FolderName);
            });
        }
        Action(recentView ? "移除最近记录" : "移除入口（不删除文件）", () => RemoveById(entry.Id));
        EntryList.ContextMenu = menu;
    }
    private string? Prompt(string title, string value)
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        var window = new Window { Title = title, Owner = this, Width = 350, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        var box = new TextBox { Text = value, MaxLength = 80 };
        var button = new Button { Content = "确定", IsDefault = true, Margin = new Thickness(0, 12, 0, 0) };
        button.Click += (_, _) => { if (box.Text.Trim().Length > 0) window.DialogResult = true; };
        panel.Children.Add(box);
        panel.Children.Add(button);
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return window.ShowDialog() == true ? box.Text.Trim() : null;
    }
}
