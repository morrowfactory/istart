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
    private bool exitRequested;
    private bool presentingMenu;
    private bool searchFocusPending;
    private string SessionMarker => Path.Combine(store.DirectoryPath, "session.active");
    private void InitializeShell()
    {
        adapter = new StartMenuAdapter(Dispatcher, ToggleMenu);
        adapter.StatusChanged += message => { RuntimeLog.Write("adapter-status " + message); StatusText.Text = message; };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 iStart", null, (_, _) => ShowMenu(null));
        menu.Items.Add("切回 Windows 原生菜单", null, (_, _) => { SetNativeMode(); OpenNative(this, new RoutedEventArgs()); });
        menu.Items.Add("设置", null, (_, _) => { ShowMenu(null); OpenSettings(this, new RoutedEventArgs()); });
        menu.Items.Add("退出 iStart", null, (_, _) => RequestExit("tray"));
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
        };
        Deactivated += (_, _) =>
        {
            if (!presentingMenu && adapter.Enabled && OwnedWindows.Count == 0 && !IsMouseCaptureWithin) Hide();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Topmost = false; };
        Activated += (_, _) => QueueSearchFocus();
        Closing += (_, e) =>
        {
            RuntimeLog.Write($"window-closing explicit={exitRequested} sessionEnding={App.IsSessionEnding}");
            if (exitRequested || App.IsSessionEnding) return;
            e.Cancel = true;
            Hide();
            RuntimeLog.Write("window-hidden-to-tray");
        };
        Closed += (_, _) =>
        {
            RuntimeLog.Write("window-closed");
            adapter.Dispose();
            tray.Dispose();
            undoTimer?.Stop();
            try { if (File.Exists(SessionMarker)) File.Delete(SessionMarker); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        };
    }
    internal void RequestExit(string reason)
    {
        RuntimeLog.Write("exit-request " + reason);
        exitRequested = true;
        Close();
    }
    private async Task<bool> SetCustomMode()
    {
        if (adapter is null) return false;
        try
        {
            Directory.CreateDirectory(store.DirectoryPath);
            File.WriteAllText(SessionMarker, "active");
            await adapter.Enable();
            RuntimeLog.Write("adapter-enabled");
            store.Change(s => s.CustomMenuEnabled = true);
            ModeStatus.Text = "自定义菜单 · 试用";
            StatusText.Text = "开始按钮和 Win 键已接入。原生菜单和托盘恢复入口随时可用。";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            RuntimeLog.Write($"adapter-enable-failed {ex.GetType().Name} 0x{ex.HResult:X8}");
            SetNativeMode();
            StatusText.Text = ex is NotSupportedException ? ex.Message : "入口启用失败，已恢复 Windows 原生菜单。";
            return false;
        }
    }
    private void SetNativeMode()
    {
        RuntimeLog.Write("native-mode");
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
        if (IsVisible && (IsActive || Topmost)) Hide(); else ShowMenu(point);
    }
    internal void ShowMenu(WinInput.Point? point)
    {
        presentingMenu = true;
        try
        {
            WindowState = WindowState.Normal;
            ShowActivated = true;
            // 菜单显示期间使用浮层，收起时解除；不修改系统前台锁定策略。
            Topmost = true;
            Show();
            if (recentView) _ = RefreshRecentDocuments();
            if (adapter?.Enabled == true)
            {
                var screen = point is WinInput.Point click
                    ? Forms.Screen.FromPoint(new System.Drawing.Point(click.X, click.Y))
                    : Forms.Screen.FromHandle(WinInput.GetForegroundWindow());
                var target = adapter.Targets.FirstOrDefault(t => Forms.Screen.FromHandle(t.Taskbar).DeviceName == screen.DeviceName)
                    ?? adapter.Targets.FirstOrDefault();
                if (target is not null) screen = Forms.Screen.FromHandle(target.Taskbar);
                var area = screen.WorkingArea;
                var handle = new WindowInteropHelper(this).Handle;
                if (WinInput.GetWindowRect(handle, out var rect))
                {
                    var position = MenuPlacement.Calculate(new Rect(area.X, area.Y, area.Width, area.Height),
                        target?.Bounds ?? new Rect(area.Left, area.Bottom, 0, 0), new Size(rect.Right - rect.Left, rect.Bottom - rect.Top));
                    WinInput.SetWindowPos(handle, 0, (int)position.X, (int)position.Y, (int)position.Width, (int)position.Height, 0x4);
                }
            }
            var windowHandle = new WindowInteropHelper(this).Handle;
            WinInput.SetForegroundWindow(windowHandle);
            Activate();
            if (adapter?.Enabled != true) Topmost = false;
            QueueSearchFocus();
            RuntimeLog.Write($"menu-shown foreground={WinInput.GetForegroundWindow() == windowHandle} topmost={Topmost}");
            if (adapter?.Enabled == true && WinInput.GetForegroundWindow() != windowHandle)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                {
                    if (!IsVisible || !Topmost) return;
                    presentingMenu = true;
                    try
                    {
                        var focused = WinInput.TryForegroundAfterMenuInput(windowHandle);
                        if (focused) { Activate(); QueueSearchFocus(); }
                        RuntimeLog.Write($"menu-focus-retry foreground={focused}");
                    }
                    finally { presentingMenu = false; }
                });
        }
        finally { presentingMenu = false; }
    }
    private void QueueSearchFocus()
    {
        if (searchFocusPending) return;
        searchFocusPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            searchFocusPending = false;
            if (!IsVisible || OwnedWindows.Count > 0) return;
            var handle = new WindowInteropHelper(this).Handle;
            if (WinInput.GetForegroundWindow() != handle) return;
            WinInput.SetFocus(handle);
            System.Windows.Input.FocusManager.SetFocusedElement(this, SearchBox);
            SearchBox.Focus();
            System.Windows.Input.Keyboard.Focus(SearchBox);
            RuntimeLog.Write($"search-focus active={IsActive} keyboard={SearchBox.IsKeyboardFocused} native={WinInput.GetFocus() == handle} modifiers={WinInput.ModifiersHeld} win={((WinInput.GetAsyncKeyState(0x5B) | WinInput.GetAsyncKeyState(0x5C)) & 0x8000) != 0}");
        });
    }
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(22) };
        var window = new Window { Owner = this, Title = "iStart 设置", Width = 430, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel, Background = Background, Foreground = Foreground, FontFamily = FontFamily };
        window.SetResourceReference(BackgroundProperty, "Surface");
        window.SetResourceReference(ForegroundProperty, "TextPrimary");
        panel.Children.Add(new TextBlock { Text = "外观风格", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var theme = new ComboBox { ItemsSource = new[] { "Windows 风格", "Google 风格" }, SelectedIndex = store.Library.Theme == "Windows" ? 0 : 1, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 7, 10, 7) };
        System.Windows.Automation.AutomationProperties.SetName(theme, "外观风格");
        panel.Children.Add(theme);
        panel.Children.Add(new TextBlock { Text = "选择后立即应用并记住，不改变开始菜单接入模式。", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 18) });
        theme.SelectionChanged += (_, _) =>
        {
            var selected = theme.SelectedIndex == 0 ? "Windows" : "Google";
            if (selected == store.Library.Theme) return;
            Perform(() => SetTheme(selected), "外观已切换并保存。");
            theme.SelectedIndex = store.Library.Theme == "Windows" ? 0 : 1;
        };
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
        var windowsDocuments = Option("显示其他软件最近文件（仅本机，不读取文件内容）", store.Library.ShowWindowsRecentDocuments);
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
                store.Change(s => { s.StartWithWindows = startup.IsChecked == true; s.RecordLaunches = record.IsChecked == true; s.ShowWindowsRecentDocuments = windowsDocuments.IsChecked == true; s.RecommendationsEnabled = recommendations.IsChecked == true; });
                if (!store.Library.ShowWindowsRecentDocuments) windowsRecent.Clear();
                else if (recentView) await RefreshRecentDocuments();
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
    internal void SetTheme(string theme)
    {
        store.Change(s => s.Theme = theme);
        Appearance.Apply(theme);
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
