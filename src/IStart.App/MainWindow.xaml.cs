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
    private const string EntryFormat = "iStart.Entry";
    private const string CategoryFormat = "iStart.Category";
    private readonly LibraryStore store;
    private Point dragStart;
    private Guid? draggedEntry;
    private Guid? draggedCategory;
    private Guid? activeCategory;
    private bool ready;
    private List<SoftwareCandidate> candidates = [];
    private CancellationTokenSource? discovery;
    public MainWindow(LibraryStore store)
    {
        this.store = store;
        InitializeComponent();
        ready = true;
        RefreshCategories();
        if (store.Recovered) StatusText.Text = "主配置损坏，已读取上一份配置。请核对收藏后再修改。";
        Loaded += async (_, _) => await DiscoverSoftware();
        Closed += (_, _) => discovery?.Cancel();
        RecommendationToggle.Content = store.Library.RecommendationsEnabled ? "关闭推荐" : "启用推荐";
        if (!store.Library.RecommendationsEnabled) RecommendationStatus.Text = "推荐已关闭。你可以继续手动添加入口。";
    }

    private async Task DiscoverSoftware()
    {
        if (!store.Library.RecommendationsEnabled) return;
        discovery?.Cancel();
        var current = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        discovery = current;
        RecommendationStatus.Text = "正在本机整理软件入口…可点击关闭跳过。";
        try
        {
            candidates = await Task.Run(() => SoftwareDiscovery.Read(current.Token), current.Token).WaitAsync(current.Token);
            if (discovery != current || !store.Library.RecommendationsEnabled) return;
            RefreshRecommendations();
        }
        catch (OperationCanceledException)
        {
            if (discovery == current && store.Library.RecommendationsEnabled) RecommendationStatus.Text = "本次整理已超时；可继续手动添加，重新启用推荐后重试。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { RecommendationStatus.Text = "暂时无法读取软件入口，手动添加不受影响。"; }
    }
    private void RefreshRecommendations()
    {
        Recommendations.Children.Clear();
        if (!store.Library.RecommendationsEnabled) return;
        foreach (var candidate in candidates.Where(c => !store.Library.ExcludedRecommendations.Contains(c.Shortcut, StringComparer.OrdinalIgnoreCase)).Take(12))
        {
            var button = new Button { Width = 78, Height = 44, Padding = new Thickness(4), ToolTip = candidate.Title + "\n单击打开；右键固定或移除", Content = new TextBlock { Text = candidate.Title, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap } };
            button.Click += (_, _) => OpenTarget(candidate.Shortcut);
            var origin = new Point();
            button.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(button);
            button.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(button);
                if (Math.Abs(point.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                DragDrop.DoDragDrop(button, new DataObject(DataFormats.FileDrop, new[] { candidate.Shortcut }), DragDropEffects.Link);
            };
            var menu = new ContextMenu();
            var pin = new MenuItem { Header = "固定到当前分类" };
            pin.Click += (_, _) => AddPaths([candidate.Shortcut], activeCategory ?? Guid.Empty, null);
            var remove = new MenuItem { Header = "不再推荐" };
            remove.Click += (_, _) => Perform(() => { store.Change(s => s.ExcludedRecommendations.Add(candidate.Shortcut)); RefreshRecommendations(); }, "已移除推荐，不会自动重新出现。");
            menu.Items.Add(pin);
            menu.Items.Add(remove);
            button.ContextMenu = menu;
            Recommendations.Children.Add(button);
        }
        RecommendationStatus.Text = Recommendations.Children.Count == 0 ? "没有找到可确认的软件入口，可手动拖入。" : "按名称排列，非使用频率；右键可固定或移除。";
    }
    private async void ToggleRecommendations(object sender, RoutedEventArgs e)
    {
        Perform(() => store.Change(s => s.RecommendationsEnabled = !s.RecommendationsEnabled), "推荐设置已保存。");
        RecommendationToggle.Content = store.Library.RecommendationsEnabled ? "关闭推荐" : "启用推荐";
        if (store.Library.RecommendationsEnabled) await DiscoverSoftware();
        else { discovery?.Cancel(); Recommendations.Children.Clear(); RecommendationStatus.Text = "推荐已关闭。你可以继续手动添加入口。"; }
    }

    private void RefreshCategories()
    {
        CategoryList.ItemsSource = new[] { new MenuCategory(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), "全部收藏") }.Concat(store.Library.Categories).ToList();
        CategoryList.SelectedIndex = 0;
    }
    private void RefreshEntries()
    {
        if (!ready) return;
        var keyword = SearchBox.Text.Trim();
        EntryList.ItemsSource = store.Library.Entries.Where(e => (activeCategory is null || e.CategoryId == activeCategory)
            && (e.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || e.Target.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) => RefreshEntries();
    private void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not MenuCategory category) return;
        activeCategory = CategoryList.SelectedIndex == 0 ? null : category.Id;
        SectionTitle.Text = category.Name;
        RefreshEntries();
    }
    private void Perform(Action action, string success)
    {
        try { action(); RefreshEntries(); StatusText.Text = success; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { StatusText.Text = "操作未完成，请检查目标或配置文件权限。原文件未被移动。"; }
    }
    private void AddCategory(object sender, RoutedEventArgs e)
    {
        var name = CategoryName.Text.Trim();
        if (name.Length == 0 || name.Length > 24) { StatusText.Text = "分类名称请输入 1–24 个字。"; return; }
        if (store.Library.Categories.Any(c => c.Name == name)) { StatusText.Text = "已有同名分类。"; return; }
        Perform(() => { store.Change(s => s.Categories.Add(new(Guid.NewGuid(), name))); RefreshCategories(); CategoryName.Clear(); }, "分类已保存；拖动分类名称可以排序。");
    }
    private void AddFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "添加文件或软件快捷方式" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames, activeCategory ?? Guid.Empty, null);
    }
    private void AddPaths(string[] paths, Guid category, Guid? before)
    {
        Perform(() => store.AddPaths(paths, category, before), "入口已保存；重复或不存在的目标不会重复添加。");
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
    private void EntryMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(this);
        draggedEntry = IsButton(e.OriginalSource) ? null : ItemAt<MenuEntry>(EntryList, e.OriginalSource)?.Id;
    }
    private void EntryMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedEntry is not Guid id || !Moved(e)) return;
        draggedEntry = null;
        DragDrop.DoDragDrop(EntryList, new DataObject(EntryFormat, id.ToString()), DragDropEffects.Move);
    }
    private bool Moved(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        return Math.Abs(point.X - dragStart.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(point.Y - dragStart.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }
    private void EntryDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link
            : e.Data.GetDataPresent(EntryFormat) && SearchBox.Text.Trim().Length == 0 ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        if (e.Effects != DragDropEffects.None) StatusText.Text = "松开后插入到目标入口之前；空白处为末尾。";
    }
    private void EntryDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var target = ItemAt<MenuEntry>(EntryList, e.OriginalSource);
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), activeCategory ?? target?.CategoryId ?? Guid.Empty, target?.Id);
        else if (ReadDragId(e.Data, EntryFormat) is Guid id && store.Library.Entries.Any(item => item.Id == id) && SearchBox.Text.Trim().Length == 0)
            Perform(() => store.Move(id, activeCategory ?? target?.CategoryId ?? store.Library.Entries.Single(item => item.Id == id).CategoryId, target?.Id), "顺序已保存。");
    }
    private void CategoryMouseDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(this);
        draggedCategory = ItemAt<MenuCategory>(CategoryList, e.OriginalSource)?.Id;
    }
    private void CategoryMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedCategory is not Guid id || !Moved(e)
            || id == Guid.Empty || id == new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")) return;
        draggedCategory = null;
        DragDrop.DoDragDrop(CategoryList, new DataObject(CategoryFormat, id.ToString()), DragDropEffects.Move);
    }
    private void CategoryDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var category = ItemAt<MenuCategory>(CategoryList, e.OriginalSource);
        if (category is null || category.Id == new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")) return;
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), category.Id, null);
        else if (ReadDragId(e.Data, EntryFormat) is Guid id && store.Library.Entries.Any(item => item.Id == id)) Perform(() => store.Move(id, category.Id, null), "已移入分类。");
        else if (ReadDragId(e.Data, CategoryFormat) is Guid categoryId && store.Library.Categories.Any(item => item.Id == categoryId))
            Perform(() => { store.MoveCategory(categoryId, category.Id); RefreshCategories(); }, "分类顺序已保存。");
    }
    private static Guid? ReadDragId(IDataObject data, string format) => data.GetDataPresent(format)
        && Guid.TryParse(data.GetData(format) as string, out var id) ? id : null;
    private void CategoryDragOver(object sender, DragEventArgs e)
    {
        var category = ItemAt<MenuCategory>(CategoryList, e.OriginalSource);
        var valid = category is not null && category.Id != new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");
        e.Effects = !valid ? DragDropEffects.None : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link
            : e.Data.GetDataPresent(EntryFormat) || (e.Data.GetDataPresent(CategoryFormat) && category!.Id != Guid.Empty) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }
    private void RemoveEntry(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) Perform(() => store.Change(s => s.Entries.RemoveAll(entry => entry.Id == id)), "已移除入口，原文件保持不变。");
        e.Handled = true;
    }
    private void OpenSelected(object sender, MouseButtonEventArgs e)
    {
        var entry = ItemAt<MenuEntry>(EntryList, e.OriginalSource);
        if (entry is null || IsButton(e.OriginalSource)) return;
        if (!File.Exists(entry.Target) && !Directory.Exists(entry.Target)) { StatusText.Text = "目标已移动、删除或暂时离线，可以移除此入口后重新添加。"; return; }
        OpenTarget(entry.Target);
    }
    private void OpenTarget(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); StatusText.Text = "已交给 Windows 打开。"; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { StatusText.Text = "Windows 未能打开此入口，请检查文件关联或目标是否可用。"; }
    }
    private void OpenNative(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        Dispatcher.BeginInvoke(() => NativeMenu.Open());
    }
    private void Exit(object sender, RoutedEventArgs e) => Close();
}
