using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace IStart;

internal sealed class UpdateService : IDisposable
{
    private readonly LibraryStore store;
    private readonly Func<CancellationToken, Task<AppRelease?>> check;
    private readonly Func<AppRelease, IProgress<DownloadProgress>, CancellationToken, Task<string>> download;
    private readonly Func<string, bool, bool, Process?> install;
    private readonly Func<bool> canInstallAutomatically;
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private CancellationTokenSource? downloadingToken;
    private Task<AppRelease?>? checking;
    private Task<string>? downloading;
    private Process? installation;
    private DateTimeOffset nextCheck, nextAutomatic, lastAttempt;
    private bool running, started, automaticDownload;
    internal AppRelease? Release { get; private set; }
    internal string? Installer { get; private set; }
    internal DownloadProgress Progress { get; private set; } = new(0, 0);
    internal bool IsChecking { get; private set; }
    internal bool IsDownloading { get; private set; }
    internal bool IsInstalling { get; private set; }
    internal bool InstallStarted { get; private set; }
    internal bool HasUpdate => Release is not null;
    internal bool HasChecked { get; private set; }
    internal string? Error { get; private set; }
    internal event Action? Changed;

    internal UpdateService(LibraryStore store, Func<bool> canInstallAutomatically,
        Func<CancellationToken, Task<AppRelease?>>? check = null,
        Func<AppRelease, IProgress<DownloadProgress>, CancellationToken, Task<string>>? download = null,
        Func<string, bool, bool, Process?>? install = null)
    {
        this.store = store;
        this.canInstallAutomatically = canInstallAutomatically;
        this.check = check ?? GitHubUpdater.Check;
        this.download = download ?? ((release, progress, token) => Task.Run(() => GitHubUpdater.Download(release,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iStart", "Updates"), progress, token)));
        this.install = install ?? UpdateInstallation.Start;
        timer.Tick += async (_, _) => await Cycle(NetworkInterface.GetIsNetworkAvailable());
    }

    internal void Start()
    {
        if (started || lifetime.IsCancellationRequested) return;
        started = true;
        NetworkChange.NetworkAvailabilityChanged += NetworkChanged;
        timer.Start();
        _ = Cycle(NetworkInterface.GetIsNetworkAvailable());
    }
    private void NetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable || lifetime.IsCancellationRequested) return;
        timer.Dispatcher.BeginInvoke(() =>
        {
            if (lifetime.IsCancellationRequested) return;
            if (DateTimeOffset.UtcNow - lastAttempt >= TimeSpan.FromMinutes(2)) nextCheck = DateTimeOffset.MinValue;
            _ = Cycle(true);
        });
    }
    internal Task<AppRelease?> CheckNow()
    {
        lifetime.Token.ThrowIfCancellationRequested();
        ObserveInstallerExit();
        if (checking is { IsCompleted: false }) return checking;
        if (IsDownloading || IsInstalling || InstallStarted) return Task.FromResult(Release);
        return checking = CheckCore();
    }
    private async Task<AppRelease?> CheckCore()
    {
        IsChecking = true; Error = null; lastAttempt = DateTimeOffset.UtcNow; Notify();
        try
        {
            var release = await check(lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (release?.Version != Release?.Version) Installer = null;
            Release = release;
            HasChecked = true;
            nextCheck = DateTimeOffset.UtcNow.AddHours(6);
            return release;
        }
        catch (Exception ex) when (Expected(ex))
        {
            Error = "暂时无法检查更新，联网后会自动重试。";
            nextCheck = DateTimeOffset.UtcNow.AddMinutes(15);
            throw;
        }
        finally { IsChecking = false; Notify(); }
    }
    internal Task<string> DownloadNow(bool automatic = false)
    {
        lifetime.Token.ThrowIfCancellationRequested();
        if (downloading is { IsCompleted: false }) return downloading;
        if (Installer is not null) return Task.FromResult(Installer);
        return downloading = DownloadCore(automatic);
    }
    private async Task<string> DownloadCore(bool automatic)
    {
        var release = Release ?? throw new InvalidOperationException(L.T("尚未发现可升级版本。"));
        automaticDownload = automatic;
        downloadingToken?.Dispose();
        downloadingToken = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = downloadingToken.Token;
        IsDownloading = true; Error = null; Progress = new(0, release.Bytes); Notify();
        var progress = new Progress<DownloadProgress>(p => { if (!token.IsCancellationRequested) { Progress = p; Notify(); } });
        try
        {
            var path = await download(release, progress, token);
            token.ThrowIfCancellationRequested();
            return Installer = path;
        }
        catch (Exception ex) when (Expected(ex))
        {
            Error = "下载未完成；原版本仍可使用，可稍后重试。";
            nextAutomatic = DateTimeOffset.UtcNow.AddMinutes(15);
            throw;
        }
        finally { IsDownloading = false; automaticDownload = false; Notify(); }
    }
    internal async Task<bool> InstallNow(bool automatic)
    {
        lifetime.Token.ThrowIfCancellationRequested();
        ObserveInstallerExit();
        if (InstallStarted || IsInstalling || Installer is null || Release is null) return false;
        IsInstalling = true; Error = null; Notify();
        try
        {
            await GitHubUpdater.VerifyInstaller(Installer, Release, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            // 校验期间用户可能重新打开菜单或关闭自动升级；安装前重新判断。
            if (automatic && (!store.Library.AutomaticUpdates || !canInstallAutomatically())) return false;
            installation = install(Installer, store.Library.StartWithWindows, automatic);
            InstallStarted = true;
            return true;
        }
        catch (Exception ex) when (Expected(ex))
        {
            if (ex is InvalidDataException or FileNotFoundException or DirectoryNotFoundException) Installer = null;
            Error = "暂时无法安装新版；原版本仍可使用，可稍后重试。";
            nextAutomatic = DateTimeOffset.UtcNow.AddMinutes(15);
            throw;
        }
        finally { IsInstalling = false; Notify(); }
    }
    internal async Task Cycle(bool online)
    {
        ObserveInstallerExit();
        if (running || lifetime.IsCancellationRequested || InstallStarted) return;
        running = true;
        try
        {
            if (online && DateTimeOffset.UtcNow >= nextCheck) await CheckNow();
            lifetime.Token.ThrowIfCancellationRequested();
            if (!online || !store.Library.AutomaticUpdates || Release is null || DateTimeOffset.UtcNow < nextAutomatic) return;
            if (Installer is null) await DownloadNow(automatic: true);
            if (canInstallAutomatically()) await InstallNow(automatic: true);
        }
        catch (Exception ex) when (Expected(ex)) { /* 后台失败保留当前程序和新版提醒，按预算重试。 */ }
        finally { running = false; }
    }
    internal void PreferenceChanged()
    {
        if (!store.Library.AutomaticUpdates && automaticDownload) downloadingToken?.Cancel();
        nextAutomatic = DateTimeOffset.MinValue;
        Notify();
    }
    internal void CancelManualDownload()
    {
        if (!store.Library.AutomaticUpdates) downloadingToken?.Cancel();
    }
    private void ObserveInstallerExit()
    {
        if (installation is not { HasExited: true }) return;
        installation.Dispose(); installation = null; InstallStarted = false;
        nextAutomatic = DateTimeOffset.UtcNow.AddMinutes(15);
        Notify();
    }
    private void Notify() { if (!lifetime.IsCancellationRequested) Changed?.Invoke(); }
    private static bool Expected(Exception ex) => ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException
        or OperationCanceledException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or InvalidOperationException;
    public void Dispose()
    {
        timer.Stop();
        if (started) NetworkChange.NetworkAvailabilityChanged -= NetworkChanged;
        lifetime.Cancel(); downloadingToken?.Cancel();
        installation?.Dispose(); installation = null;
    }
}

internal static class UpdateInstallation
{
    internal static bool IsInstalled => !App.IsUITest && File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { internal uint Size, Time; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput value);
    internal static bool IsIdle()
    {
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        return GetLastInputInfo(ref input) && unchecked((uint)Environment.TickCount - input.Time) >= 60_000;
    }
    internal static ProcessStartInfo Arguments(string installer, string directory, bool startup, bool automatic)
    {
        var start = new ProcessStartInfo(installer) { UseShellExecute = true };
        start.ArgumentList.Add("/DIR=" + directory.TrimEnd(Path.DirectorySeparatorChar));
        start.ArgumentList.Add(startup ? "/MERGETASKS=startup" : "/MERGETASKS=!startup");
        start.ArgumentList.Add("/NORESTART");
        start.ArgumentList.Add("/NOCLOSEAPPLICATIONS");
        if (automatic)
        {
            start.ArgumentList.Add("/SILENT");
            start.ArgumentList.Add("/SP-");
            start.ArgumentList.Add("/AUTOMATICUPDATE=1");
        }
        return start;
    }
    internal static Process? Start(string installer, bool startup, bool automatic)
    {
        if (!IsInstalled) throw new InvalidOperationException(L.T("当前是开发或验收运行，请使用正式安装版完成应用内升级。"));
        return Process.Start(Arguments(installer, AppContext.BaseDirectory, startup, automatic))
            ?? throw new InvalidOperationException(L.T("无法启动升级安装器，请稍后重试。"));
    }
}
