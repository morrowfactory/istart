using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace IStart;

// No shown windows, global hooks, synthesized input, or personal configuration writes.
internal static partial class ReliabilityTest
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        var report = new List<string>();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowReads = 0;
        using var icons = new IconReader(path =>
        {
            if (path == "slow")
            {
                Interlocked.Increment(ref slowReads);
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            if (path == "broken") throw new IOException("fixture failure");
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            image.Freeze();
            return image;
        });
        try
        {
            var watch = Stopwatch.StartNew();
            var slow = icons.Get("slow");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(ReferenceEquals(slow, icons.Get("SLOW")), "duplicate requests must share one icon read");
            var heartbeat = false;
            await Application.Current.Dispatcher.InvokeAsync(() => heartbeat = true, DispatcherPriority.Input);
            Check(heartbeat && !slow.IsCompleted, "a stalled Shell read must not block UI dispatch");
            var fast = await icons.Get("fast").WaitAsync(TimeSpan.FromSeconds(3));
            Check(fast?.IsFrozen == true && !slow.IsCompleted, "one stalled icon must not block all other icons");
            report.Add($"PASS: UI dispatcher and another icon remain responsive during a stalled icon read ({watch.ElapsedMilliseconds} ms).");
            Check(await icons.Get("broken").WaitAsync(TimeSpan.FromSeconds(3)) is null, "unreadable icons use the placeholder");
            Check(await icons.Get("after-error").WaitAsync(TimeSpan.FromSeconds(3)) is not null, "icon worker survives a failed Shell read");
            release.Set();
            await slow.WaitAsync(TimeSpan.FromSeconds(3));
            Check(slowReads == 1, "one read per duplicate icon");
            report.Add("PASS: duplicate icon requests coalesce; failures do not terminate the worker.");
        }
        finally { release.Set(); }

        using var releaseQueue = new ManualResetEventSlim();
        using var blocked = new IconReader(_ => { releaseQueue.Wait(TimeSpan.FromSeconds(10)); return null; });
        try
        {
            var requests = Enumerable.Range(0, 400).Select(i => blocked.Get("fixture-" + i)).ToArray();
            Check(requests.Count(task => !task.IsCompleted) <= 256 && requests.Any(task => task.IsCompleted),
                "icon queue must remain bounded when both workers stall");
            releaseQueue.Set();
            await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(3));
            report.Add("PASS: stalled icon queue is bounded; excess work returns without blocking.");
        }
        finally { releaseQueue.Set(); }

        foreach (var dpi in new[] { 1d, 1.25, 1.5, 2 })
        {
            var minimum = new Size(620, 540);
            var size = MenuPlacement.OpeningSize(new Size(160, 28), minimum, new DpiScale(dpi, dpi));
            Check(size.Width == minimum.Width * dpi && size.Height == minimum.Height * dpi,
                "restored menu must not reuse the minimized native rectangle");
            var work = new Rect(-1920, 0, 1920, 1032);
            Check(work.Contains(MenuPlacement.Calculate(work, size)),
                "restored menu must fit the physical work area");
        }
        report.Add("PASS: minimum opening size at 100/125/150/200% DPI; negative monitor coordinates.");
        var boundImage = new System.Windows.Controls.Image();
        ShellIcon.SetTarget(boundImage, Environment.ProcessPath!);
        var placeholder = boundImage.Source;
        for (var i = 0; i < 100 && ReferenceEquals(boundImage.Source, placeholder); i++) await Task.Delay(50);
        Check(boundImage.Source is BitmapSource { IsFrozen: true } && !ReferenceEquals(boundImage.Source, placeholder),
            "the async icon must replace its placeholder on the UI dispatcher");
        report.Add("PASS: a real executable icon replaces its placeholder on the UI dispatcher.");
        var window = new MainWindow(new LibraryStore(Path.Combine(output, "fixture", "library.json")), false);
        try
        {
            var first = window.DiscoverSoftware();
            Check(ReferenceEquals(first, window.DiscoverSoftware()), "repeated openings share the active discovery");
            await first.WaitAsync(TimeSpan.FromSeconds(8));
            if (window.candidates.Count > 0)
                Check(window.DiscoverSoftware().IsCompleted, "recent discovery must be reused without waiting for another scan");
            report.Add("PASS: software discovery shares in-flight work and reuses a successful recent result.");
        }
        finally { window.Close(); }
        await AdapterRecoveryTest.Run();
        report.Add("PASS: taskbar snapshot invalidation, retry recovery, cancelled enable, and unsupported-build native fallback (no hooks installed).");
        report.Add(await InstanceChannelTest.Run());
        report.Add("Scope: no windows shown, global input hooks, synthetic keys/clicks, installation, or personal configuration changes. Actual Win/Start opening and reported text deletion remain unverified.");
        File.WriteAllLines(Path.Combine(output, "reliability-result.txt"), report);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
