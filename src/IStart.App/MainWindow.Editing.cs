using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace IStart;

public partial class MainWindow
{
    private MenuEntry? removedEntry;
    private int removedIndex;
    private DispatcherTimer? undoTimer;
    private ListBoxItem? dropTarget;
    private DataTemplate? listTemplate;
    private ItemsPanelTemplate? listPanel;
    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "添加文件夹入口", Multiselect = true };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FolderNames, activeCategory ?? Guid.Empty, null);
    }
    private void ToggleView(object sender, RoutedEventArgs e)
    {
        Perform(() => store.Change(s => s.IconView = !s.IconView), "显示方式已保存。拖动图标或列表条目可以调整顺序。");
        ApplyView();
    }
    private void ApplyView()
    {
        listTemplate ??= EntryList.ItemTemplate;
        listPanel ??= EntryList.ItemsPanel;
        EntryList.ItemTemplate = store.Library.IconView && !recentView ? (DataTemplate)Resources["EntryTile"] : listTemplate;
        var panel = store.Library.IconView && !recentView ? (ItemsPanelTemplate)Resources["IconPanel"] : listPanel;
        EntryList.ItemsPanel = panel;
        EntryList.GroupStyle[0].Panel = listPanel;
        ViewToggle.Content = store.Library.IconView ? "列表" : "图标";
    }
    private void RemoveById(Guid id)
    {
        if (recentView)
        {
            var item = EntryList.Items.OfType<MenuEntry>().FirstOrDefault(entry => entry.Id == id);
            if (item is not null) Perform(() => store.Change(s => s.Launches.RemoveAll(l => l.Target == item.Target)), "已移除此条最近记录，原文件保持不变。");
            return;
        }
        var entry = store.Library.Entries.FirstOrDefault(item => item.Id == id);
        if (entry is null) return;
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
    private void HighlightDropTarget(MenuEntry? entry)
    {
        ClearDropTarget();
        if (entry is null || EntryList.ItemContainerGenerator.ContainerFromItem(entry) is not ListBoxItem item) return;
        dropTarget = item;
        item.BorderBrush = new SolidColorBrush(Color.FromRgb(22, 117, 232));
        item.BorderThickness = store.Library.IconView ? new Thickness(3, 0, 0, 0) : new Thickness(0, 3, 0, 0);
    }
    private void ClearDropTarget()
    {
        dropTarget?.ClearValue(Control.BorderBrushProperty);
        dropTarget?.ClearValue(Control.BorderThicknessProperty);
        dropTarget = null;
    }
    private void EntryDragLeave(object sender, DragEventArgs e) => ClearDropTarget();
    private void EntryKeyDown(object sender, KeyEventArgs e)
    {
        if (EntryList.SelectedItem is not MenuEntry entry) return;
        if (e.Key == Key.Enter) { OpenTarget(entry.Target, entry.Title); e.Handled = true; }
        else if (e.Key == Key.Delete) { RemoveById(entry.Id); e.Handled = true; }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.Up or Key.Down && !recentView && SearchBox.Text.Trim().Length == 0)
        {
            var items = EntryList.Items.OfType<MenuEntry>().ToList();
            var index = items.FindIndex(item => item.Id == entry.Id);
            var newIndex = index + (e.Key == Key.Up ? -1 : 1);
            if (newIndex < 0 || newIndex >= items.Count) return;
            var before = e.Key == Key.Up ? items[newIndex].Id : index + 2 < items.Count ? items[index + 2].Id : (Guid?)null;
            Perform(() => store.Move(entry.Id, entry.CategoryId, before), "顺序已保存。");
            EntryList.SelectedItem = store.Library.Entries.First(item => item.Id == entry.Id);
            e.Handled = true;
        }
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
        if (recentView) Action("固定到未分类", () => AddPaths([entry.Target], Guid.Empty, null));
        else
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
            var move = Action("移到分类", () => { });
            foreach (var category in store.Library.Categories)
            {
                var item = new MenuItem { Header = category.Name };
                item.Click += (_, _) => Perform(() => store.Move(entry.Id, category.Id, null), "已移入分类。");
                move.Items.Add(item);
            }
        }
        Action(recentView ? "移除最近记录" : "移除入口（不删除文件）", () => RemoveById(entry.Id));
        EntryList.ContextMenu = menu;
    }
    private void CategoryContextMenu(object sender, ContextMenuEventArgs e)
    {
        var category = ItemAt<MenuCategory>(CategoryList, e.OriginalSource) ?? CategoryList.SelectedItem as MenuCategory;
        if (category is null || category.Id == AllCategory || category.Id == RecentCategory || category.Id == Guid.Empty) { e.Handled = true; return; }
        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "重命名分类" };
        rename.Click += (_, _) =>
        {
            var name = Prompt("分类名称", category.Name);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 24) return;
            if (store.Library.Categories.Any(c => c.Id != category.Id && c.Name == name)) { StatusText.Text = "已有同名分类。"; return; }
            Perform(() => { store.RenameCategory(category.Id, name); RefreshCategories(); }, "分类名称已保存。");
        };
        var delete = new MenuItem { Header = "删除分类（入口移至未分类）" };
        delete.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "删除此分类？其中入口会移至未分类，原文件保持不变。", "删除分类", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Perform(() => { store.DeleteCategory(category.Id); RefreshCategories(); }, "分类已删除，入口已移至未分类。");
        };
        menu.Items.Add(rename);
        foreach (var direction in new[] { -1, 1 })
        {
            var move = new MenuItem { Header = direction < 0 ? "上移" : "下移" };
            move.Click += (_, _) =>
            {
                var index = store.Library.Categories.FindIndex(c => c.Id == category.Id);
                if ((direction < 0 && index <= 1) || (direction > 0 && index == store.Library.Categories.Count - 1)) return;
                var before = direction < 0 ? store.Library.Categories[index - 1].Id : index + 2 < store.Library.Categories.Count ? store.Library.Categories[index + 2].Id : (Guid?)null;
                Perform(() => { store.MoveCategory(category.Id, before); RefreshCategories(); }, "分类顺序已保存。");
            };
            menu.Items.Add(move);
        }
        menu.Items.Add(delete);
        CategoryList.ContextMenu = menu;
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
