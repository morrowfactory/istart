using System.Windows;

namespace IStart;

internal static class AdapterRecoveryTest
{
    internal static async Task Run()
    {
        var step = 0;
        var changes = 0;
        var first = new StartTarget(1, new Rect(10, 10, 40, 40));
        var moved = new StartTarget(2, new Rect(100, 10, 40, 40));
        using var adapter = new StartMenuAdapter(Application.Current.Dispatcher, _ => { }, () => step switch
        {
            0 => [first],
            1 => [],
            2 => throw new InvalidOperationException("模拟任务栏自动化对象暂时失效"),
            _ => [moved]
        });
        adapter.StatusChanged += _ => changes++;
        await adapter.RefreshTargets();
        if (adapter.Targets.Single() != first) throw new InvalidOperationException("首次任务栏快照无效");
        step = 1;
        await adapter.RefreshTargets();
        if (adapter.Targets.Single() != first || changes != 1) throw new InvalidOperationException("空结果应保留有期限的旧快照");
        step = 2;
        await adapter.RefreshTargets();
        if (changes != 1) throw new InvalidOperationException("连续失败不应重复报告状态");
        step = 3;
        await adapter.RefreshTargets();
        if (adapter.Targets.Single() != moved || changes != 2) throw new InvalidOperationException("任务栏重建后应更新快照并报告恢复");
        adapter.Disable();
        if (adapter.Targets.Count != 0) throw new InvalidOperationException("显式停用应清空快照");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<StartTarget[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var delayed = new StartMenuAdapter(Application.Current.Dispatcher, _ => { }, () =>
        {
            entered.TrySetResult();
            return release.Task.GetAwaiter().GetResult();
        });
        var refresh = delayed.RefreshTargets();
        await entered.Task;
        delayed.ShellChanged();
        release.TrySetResult([first]);
        await refresh;
        if (delayed.Targets.Count != 0) throw new InvalidOperationException("任务栏变化前启动的扫描不能覆盖失效状态");
        await delayed.RefreshTargets();
        if (delayed.Targets.Single() != first) throw new InvalidOperationException("任务栏变化后新扫描应恢复");
        var starting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueScan = new TaskCompletionSource<StartTarget[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelled = new StartMenuAdapter(Application.Current.Dispatcher, _ => { }, () =>
        {
            starting.TrySetResult();
            return continueScan.Task.GetAwaiter().GetResult();
        }, new Version(10, 0, 26200));
        var enable = cancelled.Enable();
        if (!ReferenceEquals(enable, cancelled.Enable())) throw new InvalidOperationException("并发启用必须共享同一个任务");
        await starting.Task;
        cancelled.Disable();
        continueScan.TrySetResult([first]);
        try { await enable; throw new InvalidOperationException("停用后不能继续安装钩子"); }
        catch (NotSupportedException) { }
        if (cancelled.Enabled) throw new InvalidOperationException("取消启用后必须保持原生入口");
        var unsupportedScan = false;
        using var unsupported = new StartMenuAdapter(Application.Current.Dispatcher, _ => { },
            () => { unsupportedScan = true; return [first]; }, new Version(10, 0, 26300));
        try { await unsupported.Enable(); throw new InvalidOperationException("未知系统版本不能进入接管"); }
        catch (NotSupportedException) { }
        if (unsupportedScan || unsupported.Enabled) throw new InvalidOperationException("未知系统版本必须在扫描和安装钩子前保留原生入口");
    }
}
