using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IStart;

internal static partial class SmokeTest
{
    private static void VerifyAutomaticUpdates(string output, string fixture)
    {
        Task.Run(() => VerifyUpdateLifecycle(fixture)).GetAwaiter().GetResult();
        var config = Path.Combine(fixture, "update-ui.json");
        var store = new LibraryStore(config);
        store.Change(s => s.Language = "zh-CN");
        var release = UpdateFixture(fixture);
        var window = new MainWindow(store, false, _ => Task.FromResult<AppRelease?>(release));
        var settings = window.CreateSettings();
        var panel = (StackPanel)((ScrollViewer)settings.Content).Content;
        var updateSection = panel.Children.OfType<StackPanel>().Single(p => p.Name == "UpdateSection");
        Check(panel.Children[0] == updateSection, "升级区在设置顶部，无需滚动寻找提醒");
        var upgradeBadge = ((Panel)updateSection.Children.OfType<Button>().Single().Content)
            .Children.OfType<System.Windows.Shapes.Ellipse>().Single();
        var mainBadge = (FrameworkElement)window.FindName("SettingsUpdateBadge");
        Check(mainBadge.Visibility == Visibility.Collapsed && upgradeBadge.Visibility == Visibility.Collapsed, "未发现新版时两处红点隐藏");
        window.Updates.CheckNow().GetAwaiter().GetResult();
        Check(mainBadge.Visibility == Visibility.Visible && upgradeBadge.Visibility == Visibility.Visible, "后台发现新版后同步显示设置和升级红点");
        var automatic = updateSection.Children.OfType<CheckBox>().Single(c => c.Name == "AutomaticUpdatesChoice");
        Check(automatic.IsChecked == true, "自动升级默认勾选");
        automatic.IsChecked = false;
        automatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!new LibraryStore(config).Library.AutomaticUpdates, "手动升级立即保存，重开保留");
        foreach (var language in new[] { "zh-CN", "en" })
        foreach (var theme in new[] { "Windows", "Google" })
        {
            panel.Children.OfType<ComboBox>().Single(c => c.Name == "LanguageChoice").SelectedValue = language;
            panel.Children.OfType<ComboBox>().Single(c => c.Name == "ThemeChoice").SelectedIndex = theme == "Windows" ? 0 : 1;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            var upgrade = new UpdateWindow(settings, window.Updates);
            var content = (StackPanel)((ScrollViewer)upgrade.Content).Content;
            Check(content.Children.OfType<TextBlock>().Single(t => t.Name == "CurrentVersion").Text.Contains(GitHubUpdater.CurrentVersion)
                && content.Children.OfType<TextBlock>().Single(t => t.Name == "TargetVersion").Text.Contains(release.Version), "升级页显示当前与目标版本");
            Check(content.Children.OfType<TextBox>().Single().Text.Contains("自动升级")
                && !content.Children.OfType<TextBox>().Single().Text.Contains("来源提交"), "显示功能说明，不混入发布流程尾注");
            RenderLanguage((FrameworkElement)window.Content, 712, 604, Path.Combine(output, $"update-menu-{language}-{theme}.png"));
            RenderLanguage((FrameworkElement)settings.Content, 454, 1180, Path.Combine(output, $"update-settings-{language}-{theme}.png"));
            RenderLanguage((FrameworkElement)upgrade.Content, 510, 560, Path.Combine(output, $"update-page-{language}-{theme}.png"));
            upgrade.Close();
            Check(window.Updates.HasUpdate && mainBadge.Visibility == Visibility.Visible, "关闭升级页不清除新版提醒");
        }
        var emptyChecks = 0;
        using (var noUpdate = new UpdateService(store, () => false, _ => { emptyChecks++; return Task.FromResult<AppRelease?>(null); }))
        {
            noUpdate.CheckNow().GetAwaiter().GetResult();
            var upgrade = new UpdateWindow(settings, noUpdate);
            upgrade.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(emptyChecks == 2, "主动打开检查更新时刷新无新版的旧检查结果");
            upgrade.Close();
        }
        settings.Close(); window.Close(); L.Apply("zh-CN"); Appearance.Apply("Google");
        File.WriteAllText(Path.Combine(output, "update-result.txt"), "PASS：默认自动与旧配置迁移、手动保存、断网重连与检查去重、后台下载和空闲安装、取消/退出、安装前二次校验、两处红点、当前/目标版本和更新说明、中英文双风格渲染。仅使用夹具安装包和安装回调，不执行安装器、不覆盖日常安装。\n");
    }

    private static AppRelease UpdateFixture(string fixture)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("update fixture — never execute");
        File.WriteAllBytes(Path.Combine(fixture, "update-fixture.exe"), bytes);
        return new("9.0.0-preview.1", "## 更新内容\n- 自动升级和新版红点\n- 显示新功能\n\n来源提交：test-sha",
            new Uri(GitHubUpdater.Repository + "/releases/download/v9.0.0-preview.1/iStart-Setup-9.0.0-preview.1.exe"),
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static async Task VerifyUpdateLifecycle(string fixture)
    {
        var config = Path.Combine(fixture, "update-lifecycle.json");
        File.WriteAllText(config, "{\"SchemaVersion\":2}");
        var store = new LibraryStore(config);
        Check(store.Library.AutomaticUpdates, "旧配置缺少更新选项时默认自动");
        var release = UpdateFixture(fixture);
        var path = Path.Combine(fixture, "update-fixture.exe");
        var checks = 0; var downloads = 0; var installs = 0; var eligible = false;
        using (var service = new UpdateService(store, () => eligible,
            _ => { checks++; return Task.FromResult<AppRelease?>(release); },
            (_, _, _) => { downloads++; return Task.FromResult(path); },
            (_, _, automatic) => { Check(automatic, "后台安装使用自动参数"); installs++; return null; }))
        {
            await service.Cycle(false);
            Check(checks == 0 && downloads == 0, "断网时不发起检查或下载");
            store.Change(s => s.AutomaticUpdates = false);
            await service.Cycle(true);
            Check(checks == 1 && downloads == 0 && service.HasUpdate, "手动模式联网检查，只提醒不下载");
            await service.Cycle(true);
            Check(checks == 1, "成功后六小时内不重复检查");
            store.Change(s => s.AutomaticUpdates = true); service.PreferenceChanged();
            await service.Cycle(true);
            Check(downloads == 1 && installs == 0 && service.Installer == path, "自动下载后等待隐藏与空闲，不提前安装");
            eligible = true;
            await service.Cycle(true); await service.Cycle(true);
            Check(downloads == 1 && installs == 1 && service.InstallStarted, "允许安装时复用下载并只启动一次");
        }
        var checkGate = new TaskCompletionSource<AppRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var service = new UpdateService(store, () => false, _ => { checks++; return checkGate.Task; }))
        {
            var first = service.CheckNow(); var second = service.CheckNow();
            Check(ReferenceEquals(first, second), "后台和手动检查共用进行中的请求");
            checkGate.SetResult(release); await first;
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var service = new UpdateService(store, () => true, _ => Task.FromResult<AppRelease?>(release),
            async (_, _, token) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); return path; },
            (_, _, _) => throw new InvalidOperationException("取消后不能安装")))
        {
            var cycle = service.Cycle(true);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            store.Change(s => s.AutomaticUpdates = false); service.PreferenceChanged();
            await cycle;
            Check(service.HasUpdate && service.Installer is null && !service.IsDownloading, "切换手动取消后台下载，保留红点");
        }
        using (var service = new UpdateService(store, () => true, _ => Task.FromResult<AppRelease?>(release),
            (_, _, _) => Task.FromResult(path), (_, _, _) => { installs++; return null; }))
        {
            await service.CheckNow(); await service.DownloadNow();
            File.AppendAllText(path, "tampered");
            try { await service.InstallNow(false); throw new Exception("损坏安装包未被拒绝"); }
            catch (InvalidDataException) { Check(installs == 1 && service.Installer is null && service.HasUpdate, "安装前再次校验，拒绝篡改并允许重新下载"); }
            UpdateFixture(fixture);
            await service.DownloadNow();
            Check(await service.InstallNow(false) && installs == 2, "手动操作明确启动已验证的安装包");
        }
        var late = new TaskCompletionSource<AppRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var service = new UpdateService(store, () => true, _ => late.Task))
        {
            var task = service.CheckNow(); service.Dispose(); var notified = false;
            service.Changed += () => notified = true; late.SetResult(release);
            try { await task; throw new Exception("退出后的检查未取消"); }
            catch (OperationCanceledException) { Check(!service.HasUpdate && !notified, "退出后丢弃迟到结果，不通知或安装"); }
        }
        using (var service = new UpdateService(store, () => false, _ => throw new System.Net.Http.HttpRequestException("offline")))
        {
            await service.Cycle(true);
            Check(service.Error is not null && !service.IsChecking, "连接失败回退等待重试，不影响当前程序");
        }
        var automaticArgs = UpdateInstallation.Arguments(path, "C:\\iStart", true, true).ArgumentList;
        var manualArgs = UpdateInstallation.Arguments(path, "C:\\iStart", false, false).ArgumentList;
        Check(automaticArgs.Contains("/SILENT") && automaticArgs.Contains("/AUTOMATICUPDATE=1")
            && automaticArgs.Contains("/NORESTART") && automaticArgs.Contains("/NOCLOSEAPPLICATIONS")
            && automaticArgs.Contains("/MERGETASKS=startup"), "自动更新有进度、不重启、不关闭其他应用，保留启动选择");
        Check(!manualArgs.Contains("/SILENT") && manualArgs.Contains("/MERGETASKS=!startup"), "手动升级保留完整向导和关闭启动偏好");
        Check(GitHubUpdater.DisplayNotes("  ") == L.T("此版本暂未提供更新说明。")
            && GitHubUpdater.DisplayNotes(new string('a', 13000)).Length == 12000, "无发布说明及超长说明的有界显示");
    }
}
