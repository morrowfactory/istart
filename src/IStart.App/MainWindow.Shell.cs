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
    private System.Drawing.Icon? trayIcon;
    private bool exitRequested;
    private bool presentingMenu;
    private bool searchFocusPending;
    private bool activatingMenu;
    private int presentation;
    private HwndSource? shellMessages;
    private uint taskbarCreated;
    private string SessionMarker => Path.Combine(store.DirectoryPath, "session.active");
    private void InitializeShell()
    {
        InitializeScreenshotHotkey();
        adapter = new StartMenuAdapter(Dispatcher, ToggleMenu);
        adapter.StatusChanged += message => { RuntimeLog.Write("adapter-status " + message); StatusText.Text = message; };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L.T("打开 iStart"), null, (_, _) => ShowFromTray());
        menu.Items.Add(L.T("切回 Windows 原生菜单"), null, (_, _) => { SetNativeMode(); OpenNative(this, new RoutedEventArgs()); });
        menu.Items.Add(L.T("设置"), null, (_, _) => { ShowFromTray(); OpenSettings(this, new RoutedEventArgs()); });
        menu.Items.Add(L.T("退出 iStart"), null, (_, _) => RequestExit("tray"));
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Brand/istart.ico")).Stream)
        using (var icon = new System.Drawing.Icon(stream, 16, 16))
            trayIcon = (System.Drawing.Icon)icon.Clone();
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = L.T("iStart · 个人开始菜单"), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowFromTray();
        Loaded += async (_, _) => await InitializeStartMenu(App.EnableMenuRequested);
        Deactivated += (_, _) =>
        {
            if (!presentingMenu && !activatingMenu && OwnedWindows.Count == 0 && !IsMouseCaptureWithin) Hide();
        };
        Activated += (_, _) => QueueSearchFocus();
        ContentRendered += (_, _) => QueueSearchFocus();
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
            trayIcon.Dispose();
            undoTimer?.Stop();
            ClearSessionMarker();
        };
    }
    private void InitializeWindowMessages()
    {
        taskbarCreated = WinInput.RegisterWindowMessage("TaskbarCreated");
        SourceInitialized += (_, _) =>
        {
            shellMessages = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            shellMessages.AddHook(ShellMessage);
        };
        Closed += (_, _) => shellMessages?.RemoveHook(ShellMessage);
    }
    internal async Task InitializeStartMenu(bool enableRequested)
    {
        // 明确的用户启用请求优先于异常退出恢复；普通启动仍安全退回原生模式。
        if (File.Exists(SessionMarker) && !enableRequested)
        {
            SetNativeMode();
            StatusText.Text = L.T("上次未正常退出，此次已恢复原生模式；可在设置中重新启用。");
        }
        else if (store.Library.CustomMenuEnabled || enableRequested) await SetCustomMode();
    }
    internal async Task RequestEnableMenu()
    {
        if (exitRequested || App.IsSessionEnding) return;
        RuntimeLog.Write("enable-request existing-instance");
        await SetCustomMode();
        if (!exitRequested && !App.IsSessionEnding) ShowMenu(null);
    }
    internal void PrepareForSessionEnd()
    {
        adapter?.Disable();
        ClearSessionMarker();
        RuntimeLog.Write("session-integration-released");
    }
    private void ClearSessionMarker()
    {
        try { if (File.Exists(SessionMarker)) File.Delete(SessionMarker); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { RuntimeLog.Write("session-marker-cleanup-failed " + ex.GetType().Name); }
    }
    private nint ShellMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0112 && ((long)wParam & 0xFFF0) == 0xF120 && !presentingMenu)
        {
            handled = true;
            var request = presentation;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                if (request == presentation) ShowMenu(null);
            });
            return 0;
        }
        if (message == 0x007E || message == 0x001A || taskbarCreated != 0 && (uint)message == taskbarCreated)
            adapter?.ShellChanged();
        return 0;
    }
    internal void RequestExit(string reason)
    {
        RuntimeLog.Write("exit-request " + reason);
        exitRequested = true;
        Close();
    }
    private async Task<bool> SetCustomMode()
    {
        if (adapter is null || exitRequested || App.IsSessionEnding) return false;
        try
        {
            Directory.CreateDirectory(store.DirectoryPath);
            File.WriteAllText(SessionMarker, "active");
            await adapter.Enable();
            RuntimeLog.Write("adapter-enabled");
            store.Change(s => s.CustomMenuEnabled = true);
            ModeStatus.Text = L.T("已接入");
            IntegrationDot.Visibility = Visibility.Visible;
            EnableMenuButton.Visibility = Visibility.Collapsed;
            StatusText.Text = "";
            return true;
        }
        catch (ObjectDisposedException) when (exitRequested || App.IsSessionEnding) { return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            RuntimeLog.Write($"adapter-enable-failed {ex.GetType().Name} 0x{ex.HResult:X8}");
            if (exitRequested || App.IsSessionEnding) return false;
            SetNativeMode();
            StatusText.Text = ex is NotSupportedException ? ex.Message : L.T("入口启用失败，已恢复 Windows 原生菜单。");
            return false;
        }
    }
    private void SetNativeMode()
    {
        RuntimeLog.Write("native-mode");
        adapter?.Disable();
        ModeStatus.Text = L.T("原生菜单模式");
        IntegrationDot.Visibility = Visibility.Collapsed;
        EnableMenuButton.Visibility = Visibility.Visible;
        try
        {
            store.Change(s => s.CustomMenuEnabled = false);
            if (File.Exists(SessionMarker)) File.Delete(SessionMarker);
            StatusText.Text = L.T("已切回 Windows 原生菜单，收藏仍然保留。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { StatusText.Text = L.T("原生入口已恢复，偏好暂时无法保存。"); }
    }
    private async void EnableStartMenu(object sender, RoutedEventArgs e)
    {
        EnableMenuButton.IsEnabled = false;
        try { await SetCustomMode(); }
        finally { EnableMenuButton.IsEnabled = true; }
    }
    private void ToggleMenu(WinInput.Point? point)
    {
        if (capturing) return;
        if (IsVisible && WinInput.GetForegroundWindow() == new WindowInteropHelper(this).Handle) Hide(); else ShowMenu(point);
    }
    private void ShowFromTray() => ShowMenu(WinInput.GetCursorPos(out var point) ? point : null);
    private void PlaceMenu(Forms.Screen screen, nint handle)
    {
        var area = screen.WorkingArea;
        var position = MenuPlacement.Calculate(new Rect(area.X, area.Y, area.Width, area.Height),
            MenuPlacement.OpeningSize(new Size(Width, Height), new Size(MinWidth, MinHeight), System.Windows.Media.VisualTreeHelper.GetDpi(this)));
        WinInput.SetWindowPos(handle, 0, (int)position.X, (int)position.Y, (int)position.Width, (int)position.Height, 0x14);
    }
    internal void ShowInBackground()
    {
        var screen = Forms.Screen.FromHandle(WinInput.GetForegroundWindow());
        ShowActivated = false;
        PlaceMenu(screen, new WindowInteropHelper(this).EnsureHandle());
        Show();
        Hide();
    }
    internal void ShowMenu(WinInput.Point? point)
    {
        if (capturing || exitRequested || menuClosed || App.IsSessionEnding) return;
        PauseMenuRefresh();
        // Select the monitor before Show/Activate changes the foreground window.
        var screen = point is WinInput.Point click
            ? Forms.Screen.FromPoint(new System.Drawing.Point(click.X, click.Y))
            : Forms.Screen.FromHandle(WinInput.GetForegroundWindow());
        var started = System.Diagnostics.Stopwatch.StartNew();
        var request = ++presentation;
        activatingMenu = false;
        presentingMenu = true;
        try
        {
            WindowState = WindowState.Normal;
            ShowActivated = true;
            // 菜单显示期间使用浮层，收起时解除；不修改系统前台锁定策略。
            Topmost = true;
            var windowHandle = new WindowInteropHelper(this).EnsureHandle();
            PlaceMenu(screen, windowHandle);
            Show();
            // Moving across displays can change the window DPI during the first placement.
            PlaceMenu(screen, windowHandle);
            WinInput.SetForegroundWindow(windowHandle);
            Activate();
            QueueSearchFocus();
            RuntimeLog.Write($"menu-shown foreground={WinInput.GetForegroundWindow() == windowHandle} topmost={Topmost} elapsedMs={started.ElapsedMilliseconds} size={ActualWidth:0}x{ActualHeight:0}");
            activatingMenu = WinInput.GetForegroundWindow() != windowHandle;
            _ = RetryMenuFocus(windowHandle, request);
        }
        finally { presentingMenu = false; }
        QueueMenuRefresh(true);
    }
    private async Task RetryMenuFocus(nint handle, int request)
    {
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                await Task.Delay(60);
                if (request != presentation || !IsVisible || exitRequested || OwnedWindows.Count > 0 || capturing) return;
                if (WinInput.TryForegroundAfterMenuInput(handle))
                {
                    Activate(); QueueSearchFocus();
                    if (SearchBox.IsKeyboardFocused) return;
                }
            }
            RuntimeLog.Write($"menu-focus-retry foreground={WinInput.GetForegroundWindow() == handle} search={SearchBox.IsKeyboardFocused}");
        }
        finally { if (request == presentation) activatingMenu = false; }
    }
    private void QueueSearchFocus()
    {
        if (searchFocusPending) return;
        searchFocusPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            searchFocusPending = false;
            if (!IsVisible || OwnedWindows.Count > 0) return;
            var handle = new WindowInteropHelper(this).Handle;
            if (WinInput.GetForegroundWindow() != handle) return;
            System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
            WinInput.SetFocus(handle);
            var focusError = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            System.Windows.Input.FocusManager.SetFocusedElement(this, SearchBox);
            SearchBox.Focus();
            System.Windows.Input.Keyboard.Focus(SearchBox);
            RuntimeLog.Write($"search-focus active={IsActive} keyboard={SearchBox.IsKeyboardFocused} native={WinInput.GetFocus() == handle} enabled={WinInput.IsWindowEnabled(handle)} visible={WinInput.IsWindowVisible(handle)} error={focusError} modifiers={WinInput.ModifiersHeld} win={((WinInput.GetAsyncKeyState(0x5B) | WinInput.GetAsyncKeyState(0x5C)) & 0x8000) != 0}");
        });
    }
    private void OpenSettings(object sender, RoutedEventArgs e) => CreateSettings().ShowDialog();
    internal Window CreateSettings()
    {
        var panel = new StackPanel { Margin = new Thickness(22) };
        var window = new Window { Owner = IsLoaded ? this : null, Title = L.T("iStart 设置"), Width = 470, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel, Background = Background, Foreground = Foreground, FontFamily = FontFamily };
        window.SetResourceReference(BackgroundProperty, "Surface");
        window.SetResourceReference(ForegroundProperty, "TextPrimary");
        window.SetResourceReference(FontFamilyProperty, "MenuFont");
        panel.Children.Add(new TextBlock { Text = L.T("界面语言"), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var language = new ComboBox { Name = "LanguageChoice", SelectedValuePath = "Tag", Padding = new Thickness(10, 7, 10, 7),
            ItemsSource = new[] { new ComboBoxItem { Tag = "System", Content = L.T("跟随系统") }, new ComboBoxItem { Tag = "zh-CN", Content = "简体中文" }, new ComboBoxItem { Tag = "en", Content = "English" } }, SelectedValue = store.Library.Language };
        System.Windows.Automation.AutomationProperties.SetName(language, L.T("界面语言"));
        panel.Children.Add(language);
        panel.Children.Add(new TextBlock { Text = L.T("中文系统显示简体中文；其他系统语言显示英文。可随时手动切换。"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 8, 0, 18) });
        panel.Children.Add(new TextBlock { Text = L.T("外观风格"), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var theme = new ComboBox { Name = "ThemeChoice", ItemsSource = new[] { new ComboBoxItem { Content = L.T("Windows 风格") }, new ComboBoxItem { Content = L.T("Google 风格") } }, SelectedIndex = store.Library.Theme == "Windows" ? 0 : 1, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 7, 10, 7) };
        System.Windows.Automation.AutomationProperties.SetName(theme, L.T("外观风格"));
        panel.Children.Add(theme);
        panel.Children.Add(new TextBlock { Text = L.T("截屏快捷键"), Margin = new Thickness(0, 10, 0, 6) });
        var captureShortcut = new ComboBox { ItemsSource = ScreenshotHotkey.Choices.Select(value => new ComboBoxItem { Tag = value, Content = L.T(value) }).ToArray(), SelectedValuePath = "Tag", SelectedValue = ScreenshotHotkey.Choices.Contains(store.Library.ScreenshotShortcut) ? store.Library.ScreenshotShortcut : "关闭" };
        System.Windows.Automation.AutomationProperties.SetName(captureShortcut, L.T("截屏快捷键"));
        panel.Children.Add(captureShortcut);
        var shortcutStatus = new TextBlock { Text = screenshotHotkey?.Status ?? L.T("验收模式不注册全局快捷键。"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 6, 0, 8) };
        panel.Children.Add(shortcutStatus);
        language.SelectionChanged += (_, _) =>
        {
            if (language.SelectedValue is not string selected || selected == store.Library.Language) return;
            Perform(() => SetLanguage(selected), "语言已切换并保存。");
            language.SelectedValue = store.Library.Language;
            shortcutStatus.Text = screenshotHotkey?.Status ?? L.T("验收模式不注册全局快捷键。");
        };
        captureShortcut.SelectionChanged += (_, _) =>
        {
            if (captureShortcut.SelectedValue is not string shortcut) return;
            Perform(() => store.Change(s => s.ScreenshotShortcut = shortcut), L.T("截屏快捷键已保存。"));
            screenshotHotkey?.Set(store.Library.ScreenshotShortcut);
            shortcutStatus.Text = screenshotHotkey?.Status ?? L.T("验收模式不注册全局快捷键。");
            ScreenshotButton.ToolTip = shortcutStatus.Text;
        };
        panel.Children.Add(new TextBlock { Text = L.T("选择后立即应用并记住，不改变开始菜单接入模式。"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 18) });
        theme.SelectionChanged += (_, _) =>
        {
            var selected = theme.SelectedIndex == 0 ? "Windows" : "Google";
            if (selected == store.Library.Theme) return;
            Perform(() => SetTheme(selected), L.T("外观已切换并保存。"));
            theme.SelectedIndex = store.Library.Theme == "Windows" ? 0 : 1;
        };
        panel.Children.Add(new TextBlock { Text = L.T("菜单与隐私"), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        CheckBox Option(string label, bool selected, StackPanel? container = null)
        {
            var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = selected, Margin = new Thickness(0, 7, 0, 7) };
            (container ?? panel).Children.Add(box);
            return box;
        }
        var custom = Option(L.T("使用自定义开始菜单（试用）"), adapter?.Enabled == true);
        panel.Children.Add(new TextBlock { Text = ShellCompatibility.Description(Environment.OSVersion.Version), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 10) });
        var diagnostic = new Button { Content = L.T("导出开始菜单兼容诊断"), Margin = new Thickness(0, 0, 0, 10) };
        diagnostic.Click += async (_, _) =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = L.T("保存兼容诊断"), FileName = L.T("iStart-兼容诊断.txt"), Filter = L.T("文本文件|*.txt") };
            if (dialog.ShowDialog(window) != true) return;
            diagnostic.IsEnabled = false;
            try
            {
                var report = await ShellProbe.ReadAsync();
                report += L.F("\n当前接入：{0}", adapter?.Enabled == true);
                await File.WriteAllTextAsync(dialog.FileName, report);
                MessageBox.Show(window, L.T("诊断已保存；未更改系统设置，也未上传数据。"), "iStart");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or System.Runtime.InteropServices.COMException or InvalidOperationException)
            { MessageBox.Show(window, L.T("诊断暂未完成，请稍后重试。"), "iStart"); }
            finally { diagnostic.IsEnabled = true; }
        };
        panel.Children.Add(diagnostic);
        var startup = Option(L.T("登录 Windows 后启动 iStart"), store.Library.StartWithWindows);
        startup.IsEnabled = !App.IsUITest;
        if (App.IsUITest) startup.ToolTip = L.T("交互验收模式不修改真实开机启动项。");
        var record = Option(L.T("记录通过 iStart 打开的入口（仅本机，最多 100 项）"), store.Library.RecordLaunches);
        var windowsDocuments = Option(L.T("显示其他软件最近文件与文件夹活动（仅本机）"), store.Library.ShowWindowsRecentDocuments);
        var browserHistory = Option(L.T("读取 Chrome 和 Edge 网站历史（仅本机）"), store.Library.ShowBrowserHistory);
        panel.Children.Add(new TextBlock { Text = L.T("我的软件参考本机累计应用切换和 iStart 打开记录，最多 30 个；不代表最近一个月启动次数。"), TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        var clear = new Button { Content = L.T("清除 iStart 最近记录与软件隐藏项"), Margin = new Thickness(0, 12, 0, 8) };
        clear.Click += (_, _) =>
        {
            if (MessageBox.Show(window, L.T("清除 iStart 最近记录和软件隐藏项？已添加入口会保留；不会清除 Windows 或浏览器历史。"), L.T("清除本机记录"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Perform(() => store.Change(s => { s.Launches.Clear(); s.HiddenSoftware.Clear(); }), L.T("iStart 记录已清除，已添加入口保持不变。"));
        };
        panel.Children.Add(clear);
        var save = new Button { Content = L.T("保存设置"), Margin = new Thickness(0, 10, 0, 0) };
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            var oldStartup = store.Library.StartWithWindows;
            try
            {
                if (!App.IsUITest) SystemActions.SetStartup(startup.IsChecked == true);
                store.Change(s => { s.StartWithWindows = startup.IsChecked == true; s.RecordLaunches = record.IsChecked == true; s.ShowWindowsRecentDocuments = windowsDocuments.IsChecked == true; s.ShowBrowserHistory = browserHistory.IsChecked == true; });
                if (!store.Library.ShowBrowserHistory) websiteHistory = new([], 0, 0);
                else if (websiteView) await RefreshWebsiteHistory();
                if (!store.Library.ShowWindowsRecentDocuments) { windowsRecent.Clear(); recentFolders.Clear(); }
                else if (recentView || folderView) await RefreshRecentDocuments();
                if (custom.IsChecked == true) await SetCustomMode(); else SetNativeMode();
                if (candidates.Count == 0) await DiscoverSoftware();
                RefreshEntries();
                window.Close();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
            {
                try { if (!App.IsUITest) SystemActions.SetStartup(oldStartup); } catch (Exception rollback) when (rollback is UnauthorizedAccessException or System.Security.SecurityException) { }
                MessageBox.Show(window, L.T("设置未保存，请检查本地权限。"), "iStart");
            }
            finally { save.IsEnabled = true; }
        };
        panel.Children.Add(save);
        var updatePanel = new StackPanel { Name = "UpdateSection", Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Insert(0, updatePanel);
        updatePanel.Children.Add(new TextBlock { Text = L.T("版本与升级"), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        var versionLabel = new TextBlock();
        L.Bind(versionLabel, TextBlock.TextProperty, "当前版本 {0}", GitHubUpdater.CurrentVersion);
        updatePanel.Children.Add(versionLabel);
        var update = new Button { Margin = new Thickness(0, 8, 0, 0) };
        var updateContent = new StackPanel { Orientation = Orientation.Horizontal };
        var updateText = new TextBlock();
        L.Bind(updateText, TextBlock.TextProperty, "检查更新 / 升级");
        var updateBadge = new System.Windows.Shapes.Ellipse { Name = "UpgradeUpdateBadge", Width = 7, Height = 7, Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 45, 32)), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        updateContent.Children.Add(updateText); updateContent.Children.Add(updateBadge); update.Content = updateContent;
        var updateStatus = new TextBlock { Name = "UpdateStatus", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 7, 0, 0) };
        void RefreshUpdate()
        {
            if (!window.Dispatcher.CheckAccess()) { if (!window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke(RefreshUpdate); return; }
            updateBadge.Visibility = Updates.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
            updateStatus.Text = Updates.Release is { } release ? L.F("发现新版 {0}", release.Version) : L.T("联网后会自动检查新版本。");
            System.Windows.Automation.AutomationProperties.SetName(update, L.T("检查更新 / 升级"));
            System.Windows.Automation.AutomationProperties.SetHelpText(update, updateStatus.Text);
        }
        Updates.Changed += RefreshUpdate;
        L.Current.PropertyChanged += LanguageChanged;
        void LanguageChanged(object? source, System.ComponentModel.PropertyChangedEventArgs args) => RefreshUpdate();
        window.Closed += (_, _) => { Updates.Changed -= RefreshUpdate; L.Current.PropertyChanged -= LanguageChanged; };
        RefreshUpdate();
        update.Click += (_, _) => new UpdateWindow(window, Updates).ShowDialog();
        updatePanel.Children.Add(update);
        updatePanel.Children.Add(updateStatus);
        var automatic = Option(L.T("自动升级（推荐）"), store.Library.AutomaticUpdates, updatePanel);
        automatic.Name = "AutomaticUpdatesChoice";
        automatic.Click += (_, _) =>
        {
            Perform(() => { store.Change(s => s.AutomaticUpdates = automatic.IsChecked == true); Updates.PreferenceChanged(); }, "更新方式已保存。");
            automatic.IsChecked = store.Library.AutomaticUpdates;
        };
        updatePanel.Children.Add(new TextBlock { Text = L.T("勾选：后台下载并校验，菜单收起且空闲 1 分钟后自动安装；取消：仅提醒，手动升级。选择立即保存，不重启电脑。"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 8) });
        window.MaxHeight = SystemParameters.WorkArea.Height - 40;
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        L.BindSettings(window);
        return window;
    }
    internal void SetLanguage(string language)
    {
        store.Change(s => s.Language = L.Normalize(language));
        L.Apply(store.Library.Language);
        RefreshCategories();
        RefreshEntries();
        SectionTitle.Text = SearchBox.Text.Trim().Length > 0 ? recentView ? L.T("最近记录搜索结果") : L.T("搜索结果") : ((MenuCategory)CategoryList.SelectedItem).Name;
        ModeStatus.Text = adapter?.Enabled == true ? L.T("已接入") : L.T("原生菜单模式");
        ScreenshotButton.ToolTip = screenshotHotkey?.Status ?? L.T("隐藏菜单后拖选范围，支持彩色标注");
        RefreshUpdateIndicators();
        if (tray is not null)
        {
            tray.Text = L.T("iStart · 个人开始菜单");
            var labels = new[] { "打开 iStart", "切回 Windows 原生菜单", "设置", "退出 iStart" };
            for (var i = 0; i < labels.Length; i++) tray.ContextMenuStrip!.Items[i].Text = L.T(labels[i]);
        }
        computer = null;
        if (computerView) { RenderComputer(); QueueMenuRefresh(true); }
    }
    internal void SetTheme(string theme)
    {
        store.Change(s => s.Theme = theme);
        Appearance.Apply(theme);
        ApplyThemeLayout();
        RefreshEntries();
    }
    private void ApplyThemeLayout()
    {
        var windows = store.Library.Theme == "Windows";
        TopNavigationRow.Height = new GridLength(windows ? 44 : 0);
        NavigationColumn.Width = new GridLength(windows ? 0 : 160);
        BrandColumn.Width = new GridLength(windows ? 0 : 140);
        Grid.SetRow(CategoryList, windows ? 1 : 2);
        Grid.SetColumnSpan(CategoryList, windows ? 2 : 1);
        Grid.SetColumn(ContentCard, windows ? 0 : 1);
        Grid.SetColumnSpan(ContentCard, windows ? 2 : 1);
        CategoryList.ItemsPanel = (ItemsPanelTemplate)FindResource(windows ? "TopNavigation" : "SideNavigation");
    }
    private void AccountMenu(object sender, RoutedEventArgs e) => ShowSystemMenu((Button)sender, [(L.T("锁定"), SystemAction.Lock), (L.T("注销"), SystemAction.SignOut), (L.T("切换用户"), SystemAction.SwitchUser)]);
    private void PowerMenu(object sender, RoutedEventArgs e) => ShowSystemMenu((Button)sender, [(L.T("睡眠"), SystemAction.Sleep), (L.T("休眠"), SystemAction.Hibernate), (L.T("重启"), SystemAction.Restart), (L.T("关机"), SystemAction.ShutDown)]);
    private void ShowSystemMenu(Button button, (string Label, SystemAction Action)[] actions)
    {
        var menu = new ContextMenu { PlacementTarget = button };
        foreach (var (label, action) in actions)
        {
            if (!SystemActions.Available(action)) continue;
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) =>
            {
                if (action != SystemAction.Lock && MessageBox.Show(this, L.F("确定{0}？请先保存正在处理的工作。", label), "iStart", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                try { await SystemActions.Execute(action); }
                catch (Exception ex) when (ex is NotSupportedException or System.ComponentModel.Win32Exception or InvalidOperationException)
                { StatusText.Text = ex.Message; }
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
