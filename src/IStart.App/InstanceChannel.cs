namespace IStart;

internal static class InstanceChannel
{
    internal const string TestArgument = "--ipc-test";
    internal static string MutexName(string user) => "Local\\iStart-" + user;
    internal static string SignalName(string user, string command) => "Local\\iStart-" + command + "-" + user;

    // Null means no live receiver: continue normal startup, including crash recovery.
    internal static int? TrySignal(string user, string[] args, bool grantForeground = true)
    {
        if (args.Any(a => a is not ("--background" or "--enable-menu" or "--exit"))) return null;
        try
        {
            using var owner = Mutex.OpenExisting(MutexName(user));
            var unowned = false;
            try { unowned = owner.WaitOne(0); }
            catch (AbandonedMutexException) { unowned = true; }
            if (unowned) { owner.ReleaseMutex(); return null; }
            var command = args.Contains("--exit") ? "exit" : args.Contains("--enable-menu") ? "enable" : "show";
            using var signal = EventWaitHandle.OpenExisting(SignalName(user, command));
            if (command != "exit" && grantForeground) WinInput.AllowExistingInstanceForeground();
            signal.Set();
            if (command == "exit")
            {
                try { if (!owner.WaitOne(TimeSpan.FromSeconds(10))) return 2; }
                catch (AbandonedMutexException) { }
                owner.ReleaseMutex();
            }
            return 0;
        }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException) { return null; }
    }
}
