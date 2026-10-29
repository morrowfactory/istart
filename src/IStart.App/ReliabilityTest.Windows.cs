using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace IStart;

internal static partial class ReliabilityTest
{
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
    [DllImport("user32.dll")] private static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder value, uint length, out uint needed);
    private static string DesktopName(nint desktop)
    {
        var name = new StringBuilder(256);
        Check(desktop != 0 && GetUserObjectInformation(desktop, 2, name, 512, out _), "desktop identity must be readable");
        return name.ToString();
    }
    internal static async Task RunWindows(string output)
    {
        // Refuse to show even a test window on the user's input desktop.
        var desktop = DesktopName(GetThreadDesktop(GetCurrentThreadId()));
        Check(desktop.StartsWith("iStart-QA-", StringComparison.Ordinal), "requires a dedicated QA desktop");
        var input = OpenInputDesktop(0, false, 1);
        try { Check(desktop != DesktopName(input), "refusing the interactive input desktop"); }
        finally { if (input != 0) CloseDesktop(input); }
        Directory.CreateDirectory(output);
        var store = new LibraryStore(Path.Combine(output, "fixture", "library.json"));
        store.Change(s => { s.CustomMenuEnabled = false; s.ScreenshotShortcut = "关闭"; s.AutomaticUpdates = false; });
        var window = new MainWindow(store, false);
        var measurements = new List<double>();
        try
        {
            // Warm the bounded read before measuring display; no integration or hotkey registration.
            await window.DiscoverSoftware();
            foreach (var theme in new[] { "Windows", "Google" })
            {
                window.SetTheme(theme);
                for (var i = 0; i < 50; i++)
                {
                    if (i % 10 == 0) window.WindowState = WindowState.Minimized;
                    var started = Stopwatch.StartNew();
                    window.ShowMenu(null);
                    await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                    Check(window.IsVisible && window.WindowState == WindowState.Normal && window.Topmost,
                        $"{theme} cycle {i}: menu must be visible and restored");
                    Check(window.ActualWidth >= window.MinWidth && window.ActualHeight >= window.MinHeight,
                        $"{theme} cycle {i}: window must not collapse to a thin strip");
                    var handle = new WindowInteropHelper(window).Handle;
                    Check(WinInput.GetWindowRect(handle, out var rect) && rect.Bottom - rect.Top >= window.MinHeight,
                        $"{theme} cycle {i}: native window height must remain usable");
                    // An inactive desktop cannot establish acceptance of foreground/input on the live desktop.
                    measurements.Add(started.Elapsed.TotalMilliseconds);
                    window.Hide();
                    await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                }
            }
            measurements.Sort();
            File.WriteAllText(Path.Combine(output, "window-result.txt"),
                $"PASS: 100 show/hide cycles across Windows/Google, including ten minimized restores.\n" +
                $"Dispatch/layout duration: median {measurements[50]:F1} ms, p95 {measurements[95]:F1} ms, maximum {measurements[^1]:F1} ms.\n" +
                "Scope: separate inactive Windows desktop; no global hooks, synthetic keys/clicks, installation, or personal configuration writes. Live foreground/search input, Win interception, and text-deletion cause are not accepted by this check.\n");
        }
        finally { window.Close(); }
        SmokeTest.Run(Path.Combine(output, "core"));
    }
}
