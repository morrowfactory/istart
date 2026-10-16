using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace IStart;

// 低级钩子需要持续处理消息，不能与布局、图标提取和截图渲染共用 UI 线程。
internal sealed class InputHookThread : IDisposable
{
    internal delegate nint Callback(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, Callback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    private readonly Callback keyboardCallback, mouseCallback;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread thread;
    private Dispatcher? dispatcher;
    private volatile bool running, stopping;
    internal bool Running => running;
    internal Task Ready => ready.Task;
    internal InputHookThread(Callback keyboardCallback, Callback mouseCallback)
    {
        this.keyboardCallback = keyboardCallback;
        this.mouseCallback = mouseCallback;
        thread = new Thread(Run) { IsBackground = true, Name = "iStart input hooks" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
    private void Run()
    {
        nint keyboard = 0, mouse = 0;
        try
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            if (stopping) throw new NotSupportedException("输入接入已取消。");
            keyboard = SetWindowsHookEx(13, keyboardCallback, GetModuleHandle(null), 0);
            if (keyboard == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            mouse = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
            if (mouse == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (stopping) throw new NotSupportedException("输入接入已取消。");
            running = true;
            ready.TrySetResult();
            Dispatcher.Run();
        }
        catch (Exception ex) { ready.TrySetException(ex); RuntimeLog.Write("input-hook-thread-failed " + ex.GetType().Name); }
        finally
        {
            running = false;
            if (keyboard != 0) UnhookWindowsHookEx(keyboard);
            if (mouse != 0) UnhookWindowsHookEx(mouse);
        }
    }
    public void Dispose()
    {
        stopping = true;
        running = false;
        if (dispatcher is { HasShutdownStarted: false }) dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        if (Thread.CurrentThread != thread) thread.Join(TimeSpan.FromSeconds(2));
    }
}
