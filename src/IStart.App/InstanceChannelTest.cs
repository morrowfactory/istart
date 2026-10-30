using System.Diagnostics;
using System.Reflection;

namespace IStart;

internal static class InstanceChannelTest
{
    internal static async Task<string> Run()
    {
        if (Assembly.GetEntryAssembly()?.EntryPoint?.DeclaringType != typeof(Program)) throw new InvalidOperationException("轻量启动入口未生效");
        var id = Guid.NewGuid().ToString("N");
        var user = "QA-" + id;
        using var owner = new Mutex(true, InstanceChannel.MutexName(user));
        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceChannel.SignalName(user, "show"));
        using var enable = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceChannel.SignalName(user, "enable"));
        using var exit = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceChannel.SignalName(user, "exit"));
        var owns = true;
        var times = new List<double>();
        try
        {
            foreach (var command in new[] { "show", "show", "show", "enable" })
            {
                var watch = Stopwatch.StartNew();
                using var child = Start(id, command);
                if (!(command == "show" ? show : enable).WaitOne(TimeSpan.FromSeconds(5))) throw new InvalidOperationException("单次唤醒信号未到达");
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (child.ExitCode != 0) throw new InvalidOperationException("唤醒时加载了界面或信号处理失败：" + child.ExitCode);
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            using (var child = Start(id, "exit"))
            {
                if (!exit.WaitOne(TimeSpan.FromSeconds(5))) throw new InvalidOperationException("退出信号未到达");
                owner.ReleaseMutex(); owns = false;
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (child.ExitCode != 0) throw new InvalidOperationException("正常退出等待失败");
            }
            // Existing but unowned names must fall through to normal startup, not lose the click.
            using (var child = Start(id, "show"))
            {
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (child.ExitCode != 3 || show.WaitOne(0)) throw new InvalidOperationException("未占用互斥锁被误判为运行实例");
            }
        }
        finally { if (owns) owner.ReleaseMutex(); }

        var missing = Guid.NewGuid().ToString("N");
        using (var child = Start(missing, "show"))
        {
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            if (child.ExitCode != 3) throw new InvalidOperationException("无实例时必须保留完整启动路径");
        }
        var missingSignal = Guid.NewGuid().ToString("N");
        using (var mutex = new Mutex(true, InstanceChannel.MutexName("QA-" + missingSignal)))
        {
            try
            {
                using var child = Start(missingSignal, "show");
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (child.ExitCode != 3) throw new InvalidOperationException("退出竞争中信号缺失时必须回到正常启动");
            }
            finally { mutex.ReleaseMutex(); }
        }
        var abandonedId = Guid.NewGuid().ToString("N");
        Mutex? abandoned = null;
        var thread = new Thread(() => abandoned = new Mutex(true, InstanceChannel.MutexName("QA-" + abandonedId)));
        thread.Start(); thread.Join();
        using (abandoned)
        using (var child = Start(abandonedId, "show"))
        {
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            if (child.ExitCode != 3) throw new InvalidOperationException("异常退出互斥锁必须回到正常恢复路径");
        }
        return $"通过：隔离命名信号单次唤醒、启用、退出及失效/异常实例回退；子进程未加载 WPF/WinForms。含进程启动的唤醒检查中位数 {times.Order().ElementAt(times.Count / 2):F1} 毫秒，最大 {times.Max():F1} 毫秒（不代表真实任务栏点击全链路）。";
    }

    private static Process Start(string id, string command)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add(InstanceChannel.TestArgument);
        start.ArgumentList.Add(id);
        start.ArgumentList.Add(command);
        return Process.Start(start)!;
    }
}
