using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace IStart;

internal sealed class UpdateWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly TextBlock status = new() { Text = L.T("准备检查 GitHub 新版本…"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
    private readonly TextBox notes = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 150, Margin = new Thickness(0, 8, 0, 12) };
    private readonly ProgressBar progress = new() { Height = 7, Maximum = 100, Margin = new Thickness(0, 5, 0, 14) };
    private readonly Button action = new() { Content = L.T("检查更新") };
    private AppRelease? release;
    private string? installer;
    private readonly bool startup;

    internal UpdateWindow(Window owner, bool startup)
    {
        this.startup = startup;
        Owner = owner;
        Title = L.T("iStart 升级");
        Icon = owner.Icon;
        Width = 490;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(ForegroundProperty, "TextPrimary");
        SetResourceReference(FontFamilyProperty, "MenuFont");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = L.T("更新 iStart"), FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = L.F("当前版本 {0}", GitHubUpdater.CurrentVersion), Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(status);
        panel.Children.Add(notes);
        panel.Children.Add(progress);
        panel.Children.Add(new TextBlock { Text = L.T("从 GitHub 官方仓库下载并校验。安装包目前未签名；安装向导会显示安装进度，保留个人入口与设置。关闭窗口可取消检查或下载。"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(action);
        Content = panel;
        action.Click += async (_, _) => await RunAction();
        Loaded += async (_, _) => await RunAction();
        Closed += (_, _) => lifetime.Cancel();
    }

    private async Task RunAction()
    {
        action.IsEnabled = false;
        try
        {
            if (installer is not null)
            {
                // 开发及隔离测试版不覆盖日常安装；也不从测试界面启动安装器。
                if (App.IsUITest || !File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe")))
                { status.Text = L.T("安装包已校验。当前是开发或验收运行，请使用正式安装版完成应用内升级。"); return; }
                var start = new ProcessStartInfo(installer) { UseShellExecute = true };
                start.ArgumentList.Add("/DIR=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
                start.ArgumentList.Add(startup ? "/TASKS=startup" : "/TASKS=");
                Process.Start(start);
                // 安装器在准备安装时请求程序正常退出；用户取消向导时原程序仍能使用。
                Close();
                return;
            }
            if (release is null)
            {
                status.Text = L.T("正在连接 GitHub，检查新版本…");
                progress.IsIndeterminate = true;
                release = await GitHubUpdater.Check(lifetime.Token);
                if (release is null) { status.Text = L.T("当前已是此更新通道的最新版本。"); action.Content = L.T("重新检查"); return; }
                status.Text = L.F("发现新版本 {0} · {1:0.#} MB", release.Version, release.Bytes / 1048576d);
                notes.Text = release.Notes.Length > 12000 ? release.Notes[..12000] : release.Notes;
                action.Content = L.T("下载新版");
            }
            else
            {
                progress.IsIndeterminate = false;
                progress.Value = 0;
                status.Text = L.T("正在下载新版…");
                var updateProgress = new Progress<DownloadProgress>(p =>
                {
                    progress.Value = 100d * p.Received / p.Total;
                    status.Text = L.F("正在下载 · {0:0}%（{1:0.#} / {2:0.#} MB）", progress.Value, p.Received / 1048576d, p.Total / 1048576d);
                });
                installer = await GitHubUpdater.Download(release, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iStart", "Updates"), updateProgress, lifetime.Token);
                status.Text = L.T("下载完成，SHA256 校验通过。点击安装后进入升级向导。");
                action.Content = L.T("安装新版");
            }
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) status.Text = L.T("连接或下载超时，请重试。"); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { status.Text = ex is InvalidDataException ? ex.Message : L.T("暂时无法完成升级，请检查网络或磁盘权限后重试。原版本仍可使用。"); }
        finally { progress.IsIndeterminate = false; action.IsEnabled = true; }
    }
}
