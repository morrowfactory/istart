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
    private readonly ShellIconConverter computerIcons = new();

    private async Task RefreshComputer()
    {
        if (readingComputer || computerRead is { IsCompleted: false } || !readWebsiteSources) return;
        readingComputer = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            computerRead = Task.Run(() => ComputerItems.Read(timeout.Token), timeout.Token);
            var result = await computerRead.WaitAsync(timeout.Token);
            computer = result;
            if (computerView) RenderComputer();
        }
        catch (OperationCanceledException) { if (computerView) SearchStatus.Text = "设备读取超时；已显示的入口仍可用，重新打开此页重试。"; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { if (computerView) SearchStatus.Text = "部分系统入口暂不可读取，请重新打开此页重试。"; }
        finally { readingComputer = false; }
    }

    private void RenderComputer()
    {
        ComputerPanel.Children.Clear();
        var query = SearchBox.Text.Trim();
        bool Match(ComputerItem item) => item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Target.Contains(query, StringComparison.OrdinalIgnoreCase);
        void Heading(string title) => ComputerPanel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 8) });
        Button Card(ComputerItem item)
        {
            var content = new StackPanel();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image { Source = computerIcons.Convert(item.Target, typeof(System.Windows.Media.ImageSource), null!, System.Globalization.CultureInfo.CurrentCulture) as System.Windows.Media.ImageSource,
                Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) });
            row.Children.Add(new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(row);
            if (item.UsedPercent is double percent) content.Children.Add(new ProgressBar { Value = percent, Maximum = 100, Height = 5, Margin = new Thickness(34, 9, 0, 5) });
            if (item.Detail.Length > 0) content.Children.Add(new TextBlock { Text = item.Detail, FontSize = 11, Margin = new Thickness(34, 3, 0, 0) });
            var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(9), ToolTip = item.Title };
            button.Click += (_, _) => OpenComputerItem(item);
            return button;
        }
        Heading("设备和驱动器");
        var drives = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var item in computer?.Drives.Where(Match) ?? []) drives.Children.Add(Card(item));
        ComputerPanel.Children.Add(drives);
        if (computer is null) ComputerPanel.Children.Add(new TextBlock { Text = "正在读取硬盘分区…" });
        Heading("系统入口");
        var system = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in new[] { new ComputerItem("回收站", ComputerItems.RecycleBin), new ComputerItem("网络", ComputerItems.Network), new ComputerItem("控制面板", ComputerItems.ControlPanel) }.Where(Match))
            system.Children.Add(Card(item));
        ComputerPanel.Children.Add(system);
        var expanded = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var item in computer?.ControlPanel.Where(Match) ?? []) expanded.Children.Add(Card(item));
        if (computer?.PanelUnavailable == true) expanded.Children.Add(new TextBlock { Text = "无法展开系统项目；可点击上方控制面板打开原生窗口。", TextWrapping = TextWrapping.Wrap });
        ComputerPanel.Children.Add(new Expander { Header = "控制面板项目", IsExpanded = true, Content = expanded, Margin = new Thickness(0, 10, 0, 0) });
        SearchStatus.Text = "硬盘分区、回收站、网络与控制面板；点击打开 Windows 原生页面。";
    }

    private void OpenComputerItem(ComputerItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { item.Target } });
            StatusText.Text = "已交给 Windows 打开。";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { StatusText.Text = "Windows 暂时无法打开此项目，请检查设备或系统策略。"; }
    }
}
