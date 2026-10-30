using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace IStart;

public partial class MainWindow
{
    private static readonly Guid ComputerCategory = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private bool computerView;
    private bool readingComputer;
    private Task<ComputerSnapshot>? computerRead;
    internal ComputerSnapshot? computer;

    private async Task RefreshComputer()
    {
        if (readingComputer || computerRead is { IsCompleted: false } || !readWebsiteSources) return;
        readingComputer = true;
        var language = L.Current.Language;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            computerRead = Task.Run(() => ComputerItems.Read(timeout.Token), timeout.Token);
            var result = await computerRead.WaitAsync(timeout.Token);
            if (language != L.Current.Language) return;
            computer = result;
            if (computerView) RenderComputer();
        }
        catch (OperationCanceledException) { if (computerView) SearchStatus.Text = L.T("设备读取超时；已显示的入口仍可用，重新打开此页重试。"); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { if (computerView) SearchStatus.Text = L.T("部分系统入口暂不可读取，请重新打开此页重试。"); }
        finally
        {
            readingComputer = false;
            if (computerView && language != L.Current.Language) _ = RefreshComputer();
        }
    }

    private void RenderComputer()
    {
        ComputerPanel.Children.Clear();
        var query = SearchBox.Text.Trim();
        bool Match(ComputerItem item) => item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Target.Contains(query, StringComparison.OrdinalIgnoreCase);
        void Heading(string title) => ComputerPanel.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 4) });
        Button Card(ComputerItem item)
        {
            var content = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = new Image { Width = 18, Height = 18, Margin = new Thickness(0, 0, 6, 0) };
            ShellIcon.SetTarget(icon, item.Target);
            row.Children.Add(icon);
            var title = new TextBlock { Text = item.Title, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(title, 1);
            row.Children.Add(title);
            content.Children.Add(row);
            if (item.UsedPercent is double percent) content.Children.Add(new ProgressBar { Value = percent, Maximum = 100, Height = 3, Margin = new Thickness(0, 3, 0, 3) });
            if (item.Detail.Length > 0) content.Children.Add(new TextBlock { Text = item.Detail, FontSize = 9.5, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) });
            var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 4, 2), Padding = new Thickness(6, 4, 6, 4), ToolTip = item.Detail.Length > 0 ? item.Title + "\n" + item.Detail : item.Title };
            button.Click += (_, _) => OpenComputerItem(item);
            return button;
        }
        Heading(L.T("设备和驱动器"));
        var drives = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in computer?.Drives.Where(Match) ?? []) drives.Children.Add(Card(item));
        ComputerPanel.Children.Add(drives);
        if (computer is null) ComputerPanel.Children.Add(new TextBlock { Text = L.T("正在读取硬盘分区…") });
        Heading(L.T("系统入口"));
        var system = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in new[] { new ComputerItem(L.T("回收站"), ComputerItems.RecycleBin), new ComputerItem(L.T("网络"), ComputerItems.Network), new ComputerItem(L.T("控制面板"), ComputerItems.ControlPanel) }.Where(Match))
            system.Children.Add(Card(item));
        ComputerPanel.Children.Add(system);
        var expanded = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var item in computer?.ControlPanel.Where(Match) ?? []) expanded.Children.Add(Card(item));
        if (computer?.PanelUnavailable == true) expanded.Children.Add(new TextBlock { Text = L.T("无法展开系统项目；可点击上方控制面板打开原生窗口。"), TextWrapping = TextWrapping.Wrap });
        ComputerPanel.Children.Add(new Expander { Header = L.T("控制面板分类"), IsExpanded = true, Content = expanded, Margin = new Thickness(0, 6, 0, 0) });
        SearchStatus.Text = L.T("硬盘分区、回收站、网络与控制面板；点击打开 Windows 原生页面。");
    }

    private void OpenComputerItem(ComputerItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { item.Target } });
            StatusText.Text = L.T("已交给 Windows 打开。");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { StatusText.Text = L.T("Windows 暂时无法打开此项目，请检查设备或系统策略。"); }
    }
}
