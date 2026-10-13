using System.IO;
using System.Windows;

namespace IStart;

public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? showSignal, exitSignal;
    private RegisteredWaitHandle? showWait, exitWait;
    internal static bool EnableMenuRequested { get; private set; }
    internal static bool BackgroundRequested { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--hook-child")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RunHookChild(e.Args[1]);
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--input-test")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RunInputTest(e.Args[1]);
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--probe-shell")
        {
            Directory.CreateDirectory(e.Args[1]);
            File.WriteAllText(Path.Combine(e.Args[1], "shell-probe.txt"), ShellProbe.Read());
            Shutdown();
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            try { SmokeTest.Run(Path.GetFullPath(e.Args[1])); Shutdown(0); }
            catch (Exception ex)
            {
                Directory.CreateDirectory(e.Args[1]);
                File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), ex.ToString());
                Shutdown(1);
            }
            return;
        }
        try
        {
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            EnableMenuRequested = e.Args.Contains("--enable-menu");
            BackgroundRequested = e.Args.Contains("--background");
            showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\iStart-show-" + user);
            exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\iStart-exit-" + user);
            instance = new Mutex(true, "Local\\iStart-" + user, out var first);
            if (!first)
            {
                var exit = e.Args.Contains("--exit");
                var exitCode = 0;
                if (exit)
                {
                    exitSignal.Set();
                    try { if (instance.WaitOne(TimeSpan.FromSeconds(10))) instance.ReleaseMutex(); else exitCode = 2; }
                    catch (AbandonedMutexException) { instance.ReleaseMutex(); }
                }
                else showSignal.Set();
                instance.Dispose();
                instance = null;
                Shutdown(exitCode);
                return;
            }
            if (e.Args.Contains("--exit")) { Shutdown(); return; }
            var store = new LibraryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iStart", "library.json"));
            using (var startup = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                var configured = startup?.GetValue("iStart") is string command && command.StartsWith("\"" + Environment.ProcessPath + "\"", StringComparison.OrdinalIgnoreCase);
                if (store.Library.StartWithWindows != configured) store.Change(s => s.StartWithWindows = configured);
            }
            var window = new MainWindow(store);
            showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => Dispatcher.BeginInvoke(() => { window.Show(); window.Activate(); }), null, Timeout.Infinite, false);
            exitWait = ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) => Dispatcher.BeginInvoke(() => window.Close()), null, Timeout.Infinite, false);
            window.Show();
        }
        catch (Exception)
        {
            MessageBox.Show("暂时无法加载 iStart。请检查本地配置或文件权限。Windows 原生开始菜单仍可正常使用。", "iStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
    }
    private async void RunInputTest(string output)
    {
        try { await InputIntegrationTest.Run(output); Shutdown(); }
        catch (Exception ex) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "input-failure.txt"), ex.ToString()); Shutdown(1); }
    }
    private StartMenuAdapter? childAdapter;
    private async void RunHookChild(string output)
    {
        try
        {
            childAdapter = new StartMenuAdapter(Dispatcher, _ => { });
            await childAdapter.Enable();
            File.WriteAllText(Path.Combine(output, "child-ready.txt"), "ready");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "child-failure.txt"), ex.ToString()); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        showWait?.Unregister(null);
        childAdapter?.Dispose();
        exitWait?.Unregister(null);
        showSignal?.Dispose();
        exitSignal?.Dispose();
        instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
