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
    internal static bool IsUITest { get; private set; }
    internal static bool IsSessionEnding { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--probe-activity")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ActivityProbe.Write(Path.GetFullPath(e.Args[1]));
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) => RuntimeLog.Write($"unhandled-ui {args.Exception.GetType().Name} 0x{args.Exception.HResult:X8}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => RuntimeLog.Write($"unhandled-process {args.ExceptionObject.GetType().Name}");
        if (e.Args.Length == 2 && e.Args[0] == "--ui-test")
        {
            IsUITest = true;
            // 独立配置用于交互验收，不读取或替换个人收藏。
            var output = Path.GetFullPath(e.Args[1]);
            RuntimeLog.DirectoryPath = output;
            RuntimeLog.Write("startup ui-test");
            Directory.CreateDirectory(output);
            new MainWindow(new LibraryStore(Path.Combine(output, "library.json"))) { Title = "iStart · 交互验收" }.Show();
            return;
        }
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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RunSmokeTests(Path.GetFullPath(e.Args[1]));
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
            RuntimeLog.DirectoryPath = store.DirectoryPath;
            RuntimeLog.Write($"startup version={typeof(App).Assembly.GetName().Version} custom={store.Library.CustomMenuEnabled}");
            using (var startup = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                var configured = startup?.GetValue("iStart") is string command && command.StartsWith("\"" + Environment.ProcessPath + "\"", StringComparison.OrdinalIgnoreCase);
                if (store.Library.StartWithWindows != configured) store.Change(s => s.StartWithWindows = configured);
            }
            var window = new MainWindow(store) { ShowActivated = !BackgroundRequested };
            showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => Dispatcher.BeginInvoke(() => window.ShowMenu(null)), null, Timeout.Infinite, false);
            exitWait = ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) => Dispatcher.BeginInvoke(() => window.RequestExit("exit-signal")), null, Timeout.Infinite, false);
            window.Show();
            if (BackgroundRequested) window.Hide();
        }
        catch (Exception ex)
        {
            RuntimeLog.Write($"startup-failed {ex.GetType().Name} 0x{ex.HResult:X8}");
            MessageBox.Show("暂时无法加载 iStart。请检查本地配置或文件权限。Windows 原生开始菜单仍可正常使用。", "iStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
    }
    private async void RunSmokeTests(string output)
    {
        try
        {
            SmokeTest.Run(output);
            await AdapterRecoveryTest.Run();
            File.AppendAllText(Path.Combine(output, "result.txt"), "\nPASS：关闭收起/显式退出；任务栏空结果、扫描异常和重新定位恢复（未安装钩子、未注入按键）。\n");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
            Shutdown(1);
        }
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        IsSessionEnding = true;
        RuntimeLog.Write("session-ending " + e.ReasonSessionEnding);
        base.OnSessionEnding(e);
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
        RuntimeLog.Write("process-exit code=" + e.ApplicationExitCode);
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
