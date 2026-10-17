using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace IStart;

internal sealed class UpdateWindow : Window
{
    private readonly UpdateService updates;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TextBlock target = new() { Name = "TargetVersion", Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
    private readonly TextBox notes = new() { Name = "ReleaseNotes", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 170, Margin = new Thickness(0, 8, 0, 12) };
    private readonly ProgressBar progress = new() { Height = 7, Maximum = 100, Margin = new Thickness(0, 5, 0, 14) };
    private readonly Button action = new();
    private string? failure;

    internal UpdateWindow(Window owner, UpdateService updates)
    {
        this.updates = updates;
        if (owner.IsLoaded) Owner = owner;
        Title = L.T("iStart 升级");
        Icon = owner.Icon;
        Width = 510;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(ForegroundProperty, "TextPrimary");
        SetResourceReference(FontFamilyProperty, "MenuFont");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = L.T("更新 iStart"), FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Name = "CurrentVersion", Text = L.F("当前版本 {0}", GitHubUpdater.CurrentVersion), Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(target);
        panel.Children.Add(status);
        panel.Children.Add(new TextBlock { Text = L.T("更新内容与新功能"), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(notes);
        panel.Children.Add(progress);
        panel.Children.Add(new TextBlock { Text = L.T("从 GitHub 官方仓库下载并校验，保留个人入口与设置。手动模式关闭窗口会取消下载；自动模式继续后台下载，空闲时安装。安装包目前未签名。"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(action);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        updates.Changed += Refresh;
        action.Click += async (_, _) => await RunAction();
        Loaded += async (_, _) => { if (!updates.HasUpdate && !updates.IsDownloading) await RunAction(); };
        Closed += (_, _) => { lifetime.Cancel(); updates.CancelManualDownload(); updates.Changed -= Refresh; };
        Refresh();
    }

    private void Refresh()
    {
        if (lifetime.IsCancellationRequested) return;
        if (!Dispatcher.CheckAccess()) { if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(Refresh); return; }
        target.Text = updates.Release is { } release ? L.F("目标版本 {0} · {1:0.#} MB", release.Version, release.Bytes / 1048576d) : L.T("目标版本：检查后显示");
        notes.Text = updates.Release is { } found ? GitHubUpdater.DisplayNotes(found.Notes) : L.T("检查到新版后，这里将显示更新内容与新功能。");
        progress.IsIndeterminate = updates.IsChecking || updates.IsInstalling;
        progress.Value = updates.Progress.Total > 0 ? 100d * updates.Progress.Received / updates.Progress.Total : 0;
        status.Text = updates.IsChecking ? L.T("正在连接 GitHub，检查新版本…")
            : updates.IsDownloading ? L.F("正在下载 · {0:0}%（{1:0.#} / {2:0.#} MB）", progress.Value, updates.Progress.Received / 1048576d, updates.Progress.Total / 1048576d)
            : updates.IsInstalling ? L.T("正在重新校验并启动升级安装器…")
            : updates.InstallStarted ? L.T("升级安装器已启动，请在安装窗口查看进度。")
            : failure ?? (updates.Error is { } error ? L.T(error)
            : updates.Installer is not null ? L.T("下载完成，SHA256 校验通过。点击安装后进入升级向导。")
            : updates.HasUpdate ? L.T("新版已就绪，请查看下方更新内容。")
            : updates.HasChecked ? L.T("当前已是此更新通道的最新版本。") : L.T("准备检查 GitHub 新版本…"));
        action.Content = updates.Installer is not null ? L.T("安装新版") : updates.HasUpdate ? L.T("下载新版") : L.T("检查更新");
        action.IsEnabled = !updates.IsChecking && !updates.IsDownloading && !updates.IsInstalling && !updates.InstallStarted;
    }

    private async Task RunAction()
    {
        failure = null;
        try
        {
            if (updates.Installer is not null) { if (await updates.InstallNow(false)) Close(); }
            else if (!updates.HasUpdate) await updates.CheckNow().WaitAsync(lifetime.Token);
            else await updates.DownloadNow().WaitAsync(lifetime.Token);
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) failure = L.T("连接或下载超时，请重试。"); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { failure = ex is InvalidDataException or InvalidOperationException ? ex.Message : L.T("暂时无法完成升级，请检查网络或磁盘权限后重试。原版本仍可使用。"); }
        finally { Refresh(); }
    }
}
