using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IStart;

internal enum SystemAction { Lock, SignOut, SwitchUser, Sleep, Hibernate, Restart, ShutDown }
internal static class SystemActions
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool LockWorkStation();
    [DllImport("powrprof.dll")][return: MarshalAs(UnmanagedType.U1)] private static extern bool IsPwrHibernateAllowed();
    [DllImport("powrprof.dll")][return: MarshalAs(UnmanagedType.U1)] private static extern bool GetPwrCapabilities([Out] byte[] capabilities);
    internal static bool Available(SystemAction action)
    {
        if (action == SystemAction.Lock) return Policy("System", "DisableLockWorkstation") != 1;
        if (action == SystemAction.SwitchUser) return Policy("System", "HideFastUserSwitching") != 1 && File.Exists(Path.Combine(Environment.SystemDirectory, "tsdiscon.exe"));
        if (action == SystemAction.SignOut) return Policy("Explorer", "NoLogoff") != 1;
        if (Policy("Explorer", "NoClose") == 1) return false;
        if (action == SystemAction.Hibernate) return IsPwrHibernateAllowed();
        if (action == SystemAction.Sleep)
        {
            var capabilities = new byte[128];
            return GetPwrCapabilities(capabilities) && (capabilities[3] != 0 || capabilities[4] != 0 || capabilities[5] != 0 || capabilities[25] != 0);
        }
        return true;
    }
    private static int Policy(string section, string name)
    {
        var path = @"Software\Microsoft\Windows\CurrentVersion\Policies\" + section;
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey(path);
            if (key?.GetValue(name) is int value && value == 1) return 1;
        }
        return 0;
    }
    internal static async Task Execute(SystemAction action)
    {
        if (!Available(action)) throw new NotSupportedException("系统当前不支持或不允许此操作。");
        if (action == SystemAction.Lock) { if (!LockWorkStation()) throw new Win32Exception(); return; }
        if (action == SystemAction.Sleep)
        {
            if (!System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false)) throw new Win32Exception();
            return;
        }
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, action == SystemAction.SwitchUser ? "tsdiscon.exe" : "shutdown.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in Arguments(action)) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("系统未能启动操作。");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows 拒绝了此操作，请检查权限或未保存的工作。");
    }
    internal static string[] Arguments(SystemAction action) => action switch
    {
        SystemAction.SignOut => ["/l"], SystemAction.Hibernate => ["/h"],
        SystemAction.Restart => ["/r", "/t", "0"], SystemAction.ShutDown => ["/s", "/t", "0"],
        SystemAction.SwitchUser => [], _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("请使用安装版设置开机启动。");
            key.SetValue("iStart", "\"" + Environment.ProcessPath + "\" --background");
        }
        else key.DeleteValue("iStart", false);
    }
}
