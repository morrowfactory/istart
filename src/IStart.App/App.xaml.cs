using System.IO;
using System.Windows;

namespace IStart;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
            instance = new Mutex(true, "Local\\iStart-" + user, out var first);
            if (!first)
            {
                MessageBox.Show("iStart 已在运行，请使用已打开的窗口。", "iStart");
                instance.Dispose();
                instance = null;
                Shutdown();
                return;
            }
            var store = new LibraryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iStart", "library.json"));
            new MainWindow(store).Show();
        }
        catch (Exception)
        {
            MessageBox.Show("暂时无法加载 iStart。请检查本地配置或文件权限。Windows 原生开始菜单仍可正常使用。", "iStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
