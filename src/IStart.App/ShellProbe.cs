namespace IStart;

internal static class ShellProbe
{
    private static readonly object Gate = new();
    private static Task<string>? pending;
    internal static Task<string> ReadAsync()
    {
        lock (Gate)
        {
            if (pending is null || pending.IsCompleted) pending = Task.Run(Read);
            return pending.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }
    public static string Read()
    {
        var version = Environment.OSVersion.Version;
        var targets = StartMenuAdapter.FindTargets();
        var lines = new List<string>
        {
            $"iStart：{GitHubUpdater.CurrentVersion}", $"系统版本：{version}",
            $"64 位系统：{Environment.Is64BitOperatingSystem}",
            $"版本策略：{ShellCompatibility.Description(version)}", $"显示器数量：{System.Windows.Forms.Screen.AllScreens.Length}",
            $"开始按钮数量：{targets.Length}",
        };
        foreach (var target in targets)
            lines.Add($"识别方式：{target.Source}；按钮边界：{target.Bounds}");
        lines.Add("只读发现，不代表开始按钮、Win 键或恢复行为已经通过实机验收。未接入、未注入按键；不包含文件路径、网址或用户名称。");
        return string.Join("\n", lines);
    }
}
