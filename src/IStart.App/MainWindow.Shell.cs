using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace IStart;

public partial class MainWindow
{
    private StartMenuAdapter? adapter;
    private Forms.NotifyIcon? tray;
    private string SessionMarker => Path.Combine(store.DirectoryPath, "session.active");
    private void InitializeShell()
    {
        adapter = new StartMenuAdapter(Dispatcher, ToggleMenu);
        adapter.Failed += message => { SetNativeMode(); StatusText.Text = message; };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 iStart", null, (_, _) => ShowMenu(null));
        menu.Items.Add("切回 Windows 原生菜单", null, (_, _) => { SetNativeMode(); OpenNative(this, new RoutedEventArgs()); });
        menu.Items.Add("设置", null, (_, _) => { ShowMenu(null); OpenSettings(this, new RoutedEventArgs()); });
        menu.Items.Add("退出 iStart", null, (_, _) => Close());
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "iStart · 个人开始菜单", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowMenu(null);
        Loaded += async (_, _) =>
        {
            if (File.Exists(SessionMarker))
            {
                SetNativeMode();
                StatusText.Text = "上次未正常退出，此次已恢复原生模式；可在设置中重新启用。";
            }
            else if (store.Library.CustomMenuEnabled || App.EnableMenuRequested) await SetCustomMode();
            if (App.BackgroundRequested) Hide();
        };
        Deactivated += (_, _) => { if (adapter.Enabled && OwnedWindows.Count == 0 && !IsMouseCaptureWithin) Hide(); };
        Closed += (_, _) =>
        {
            adapter.Dispose();
            tray.Dispose();
            undoTimer?.Stop();
            try { if (File.Exists(SessionMarker)) File.Delete(SessionMarker); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        };
    }
    private async Task<bool> SetCustomMode()
    {
        if (adapter is null) return false;
        try
        {
            Directory.CreateDirectory(store.DirectoryPath);
            File.WriteAllText(SessionMarker, "active");
            await adapter.Enable();
            store.Change(s => s.CustomMenuEnabled = true);
            ModeStatus.Text = "自定义菜单 · 试用";
            StatusText.Text = "开始按钮和 Win 键已接入。原生菜单和托盘恢复入口随时可用。";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            SetNativeMode();
            StatusText.Text = ex is NotSupportedException ? ex.Message : "入口启用失败，已恢复 Windows 原生菜单。";
            return false;
        }
    }
    private void SetNativeMode()
    {
        adapter?.Disable();
        ModeStatus.Text = "原生菜单模式";
        try
        {
            store.Change(s => s.CustomMenuEnabled = false);
            if (File.Exists(SessionMarker)) File.Delete(SessionMarker);
            StatusText.Text = "已切回 Windows 原生菜单，收藏仍然保留。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { StatusText.Text = "原生入口已恢复，偏好暂时无法保存。"; }
    }
    private void ToggleMenu(WinInput.Point? point)
    {
        if (IsVisible && IsActive) Hide(); else ShowMenu(point);
    }
    private void ShowMenu(WinInput.Point? point)
    {
        WindowState = WindowState.Normal;
        Show();
        if (adapter?.Enabled == true)
        {
            WinInput.GetCursorPos(out var cursor);
            var anchor = point ?? cursor;
            var area = Forms.Screen.FromPoint(new System.Drawing.Point(anchor.X, anchor.Y)).WorkingArea;
            var handle = new WindowInteropHelper(this).Handle;
            if (WinInput.GetWindowRect(handle, out var rect))
            {
                var width = Math.Min(rect.Right - rect.Left, area.Width);
                var height = Math.Min(rect.Bottom - rect.Top, area.Height);
                var x = Math.Clamp(anchor.X - 20, area.Left, area.Right - width);
                var y = anchor.Y < area.Top ? area.Top : area.Bottom - height;
                WinInput.SetWindowPos(handle, 0, x, y, width, height, 0x4);
            }
        }
        Activate();
        SearchBox.Focus();
    }
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(22) };
        var window = new Window { Owner = this, Title = "iStart 设置", Width = 430, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel, Background = Background, Foreground = Foreground, FontFamily = FontFamily };
        panel.Children.Add(new TextBlock { Text = "菜单与隐私", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        CheckBox Option(string label, bool selected)
        {
            var box = new CheckBox { Content = label, IsChecked = selected, Margin = new Thickness(0, 7, 0, 7) };
            panel.Children.Add(box);
            return box;
        }
        var custom = Option("使用自定义开始菜单（试用）", adapter?.Enabled == true);
        panel.Children.Add(new TextBlock { Text = "当前仅在 Win11 26200 验证了基本接入；其他版本保持原生入口。", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 10) });
        var startup = Option("登录 Windows 后启动 iStart", store.Library.StartWithWindows);
        startup.IsEnabled = !App.IsUITest;
        if (App.IsUITest) startup.ToolTip = "交互验收模式不修改真实开机启动项。";
        var recommendations = Option("显示本机软件推荐", store.Library.RecommendationsEnabled);
        var record = Option("记录通过 iStart 打开的入口（仅本机，最多 100 项）", store.Library.RecordLaunches);
        panel.Children.Add(new TextBlock { Text = "未读取系统历史使用频率；推荐优先参考 iStart 自身启动记录。", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        var clear = new Button { Content = "清除最近记录与推荐偏好", Margin = new Thickness(0, 12, 0, 8) };
        clear.Click += (_, _) =>
        {
            if (MessageBox.Show(window, "清除最近记录和推荐排除列表？手动收藏与分类会保留。", "清除本机记录", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Perform(() => { store.Change(s => { s.Launches.Clear(); s.ExcludedRecommendations.Clear(); }); RefreshRecommendations(); }, "记录已清除，手动收藏保持不变。");
        };
        panel.Children.Add(clear);
        var save = new Button { Content = "保存设置", Margin = new Thickness(0, 10, 0, 0) };
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            var oldStartup = store.Library.StartWithWindows;
            try
            {
                if (!App.IsUITest) SystemActions.SetStartup(startup.IsChecked == true);
                store.Change(s => { s.StartWithWindows = startup.IsChecked == true; s.RecordLaunches = record.IsChecked == true; s.RecommendationsEnabled = recommendations.IsChecked == true; });
                if (custom.IsChecked == true) await SetCustomMode(); else SetNativeMode();
                RecommendationToggle.Content = store.Library.RecommendationsEnabled ? "关闭推荐" : "启用推荐";
                if (store.Library.RecommendationsEnabled) await DiscoverSoftware();
                else { discovery?.Cancel(); Recommendations.Children.Clear(); RecommendationStatus.Text = "推荐已关闭。"; }
                RefreshEntries();
                window.Close();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
            {
                try { if (!App.IsUITest) SystemActions.SetStartup(oldStartup); } catch (Exception rollback) when (rollback is UnauthorizedAccessException or System.Security.SecurityException) { }
                MessageBox.Show(window, "设置未保存，请检查本地权限。", "iStart");
            }
            finally { save.IsEnabled = true; }
        };
        panel.Children.Add(save);
        window.ShowDialog();
    }
    private void AccountMenu(object sender, RoutedEventArgs e) => ShowSystemMenu((Button)sender, [("锁定", SystemAction.Lock), ("注销", SystemAction.SignOut), ("切换用户", SystemAction.SwitchUser)]);
    private void PowerMenu(object sender, RoutedEventArgs e) => ShowSystemMenu((Button)sender, [("睡眠", SystemAction.Sleep), ("休眠", SystemAction.Hibernate), ("重启", SystemAction.Restart), ("关机", SystemAction.ShutDown)]);
    private void ShowSystemMenu(Button button, (string Label, SystemAction Action)[] actions)
    {
        var menu = new ContextMenu { PlacementTarget = button };
        foreach (var (label, action) in actions)
        {
            if (!SystemActions.Available(action)) continue;
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) =>
            {
                if (action != SystemAction.Lock && MessageBox.Show(this, $"确定{label}？请先保存正在处理的工作。", "iStart", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                try { await SystemActions.Execute(action); }
                catch (Exception ex) when (ex is NotSupportedException or System.ComponentModel.Win32Exception or InvalidOperationException)
                { StatusText.Text = ex.Message; }
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
