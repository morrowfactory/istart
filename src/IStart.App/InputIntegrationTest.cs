using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace IStart;

internal static class InputIntegrationTest
{
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    public static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        if (WinInput.ModifiersHeld) throw new InvalidOperationException("测试开始时有修饰键按下，请松开后重试。");
        var count = 0;
        using var adapter = new StartMenuAdapter(Application.Current.Dispatcher, _ => count++);
        var report = new List<string> { $"OS={Environment.OSVersion.Version}" };
        try
        {
            WinInput.TestKeys((0x5B, false), (0x5B, true));
            await Task.Delay(1000);
            var nativeHost = WinInput.ForegroundProcess();
            report.Add("Native baseline: " + nativeHost);
            WinInput.Keys((0x1B, false), (0x1B, true));
            await Task.Delay(300);
            await adapter.Enable();
            var probes = 0;
            using (var monitor = new InputHookThread((code, message, data) =>
            {
                if (code >= 0 && Marshal.ReadInt32(data) == 0x87) Interlocked.Increment(ref probes);
                return CallNextHookEx(0, code, message, data);
            }, (code, message, data) => CallNextHookEx(0, code, message, data)))
            {
                await monitor.Ready;
                var sendProbe = Task.Run(async () => { await Task.Delay(100); Check(WinInput.TestKeys((0x87, false), (0x87, true)), "发送 F24 线程探针"); });
                // 仅阻塞本测试进程的 UI；探针不触发截图、启动软件或修改个人配置。
                Thread.Sleep(1300);
                await sendProbe;
                Check(Volatile.Read(ref probes) >= 2, "UI 阻塞期间专用钩子仍处理输入");
                report.Add("PASS dedicated hook thread => input processed during 1300 ms UI stall");
            }
            Check(WinInput.TestKeys((0x5B, false), (0x5B, true)), "发送 Win 键");
            await Task.Delay(600);
            Check(count == 1, "单独 Win 键触发一次自定义入口");
            report.Add("PASS Win key => custom callback");
            report.Add("After Win: " + WinInput.ForegroundProcess());
            Check(WinInput.ForegroundProcess() != nativeHost, "未同时打开原生菜单");

            Check(WinInput.TestKeys((0x5B, false), (0x52, false), (0x52, true), (0x5B, true)), "发送 Win+R");
            await Task.Delay(400);
            Check(count == 1, "组合键不触发自定义菜单");
            report.Add("PASS Win+R => preserved; foreground=" + WinInput.ForegroundProcess());
            WinInput.Keys((0x1B, false), (0x1B, true));
            await Task.Delay(300);

            var target = adapter.Targets.First();
            WinInput.TestClick(new() { X = (int)(target.Bounds.Left + target.Bounds.Width / 2), Y = (int)(target.Bounds.Top + target.Bounds.Height / 2) });
            await Task.Delay(500);
            Check(count == 2, "开始按钮点击触发一次自定义入口");
            report.Add("PASS Start click => custom callback");
            Check(WinInput.ForegroundProcess() != nativeHost, "点击未同时打开原生菜单");

            NativeMenu.Open();
            await Task.Delay(1200);
            report.Add("After native request: " + WinInput.ForegroundProcess());
            Check(WinInput.ForegroundProcess() == nativeHost, "临时原生菜单可打开");
            Check(count == 2, "原生请求不递归触发自定义菜单");
            report.Add("PASS temporary native menu => " + WinInput.ForegroundProcess());
            WinInput.Keys((0x1B, false), (0x1B, true));
            adapter.Disable();
            await Task.Delay(200);
            WinInput.TestKeys((0x5B, false), (0x5B, true));
            await Task.Delay(600);
            Check(WinInput.ForegroundProcess() == nativeHost, "禁用后 Win 键恢复原生菜单");
            report.Add("PASS disable => native Win key");
            WinInput.Keys((0x1B, false), (0x1B, true));
            var ready = Path.Combine(output, "child-ready.txt");
            if (File.Exists(ready)) File.Delete(ready);
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") info.ArgumentList.Add(typeof(App).Assembly.Location);
            info.ArgumentList.Add("--hook-child"); info.ArgumentList.Add(Path.GetFullPath(output));
            using (var child = Process.Start(info) ?? throw new InvalidOperationException("无法启动恢复测试进程"))
            {
                try
                {
                    for (var i = 0; i < 50 && !File.Exists(ready) && !child.HasExited; i++) await Task.Delay(100);
                    Check(File.Exists(ready) && !child.HasExited, "子进程已接入");
                    child.Kill();
                    await child.WaitForExitAsync();
                    await Task.Delay(200);
                    WinInput.TestKeys((0x5B, false), (0x5B, true));
                    await Task.Delay(700);
                    Check(WinInput.ForegroundProcess() == nativeHost, "进程异常退出后原生入口恢复");
                    report.Add("PASS crash recovery => native Win key (only test child terminated)");
                }
                finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(); } }
            }
        }
        finally
        {
            adapter.Disable();
            WinInput.Keys((0x11, true), (0x5B, true), (0x5C, true), (0x1B, false), (0x1B, true));
            File.WriteAllLines(Path.Combine(output, "input-result.txt"), report);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
