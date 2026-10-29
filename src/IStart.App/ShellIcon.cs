using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

// Shell extensions can block; never run them from layout or a binding converter.
internal sealed class IconReader : IDisposable
{
    private readonly BlockingCollection<(string Path, TaskCompletionSource<ImageSource?> Completion)> queue = new(256);
    private readonly Dictionary<string, Task<ImageSource?>> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, ImageSource?> read;
    private readonly Thread[] workers;
    internal IconReader(Func<string, ImageSource?> read)
    {
        this.read = read;
        workers = Enumerable.Range(0, 2).Select(_ => new Thread(Run) { IsBackground = true, Name = "iStart icons" }).ToArray();
        foreach (var worker in workers) { worker.SetApartmentState(ApartmentState.STA); worker.Start(); }
    }
    internal Task<ImageSource?> Get(string path)
    {
        lock (cache)
        {
            if (cache.TryGetValue(path, out var found)) return found;
            if (queue.IsAddingCompleted) return Task.FromResult<ImageSource?>(null);
            if (cache.Count >= 256)
            {
                var completed = cache.FirstOrDefault(pair => pair.Value.IsCompleted).Key;
                if (completed is null) return Task.FromResult<ImageSource?>(null);
                cache.Remove(completed);
            }
            var completion = new TaskCompletionSource<ImageSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!queue.TryAdd((path, completion))) return Task.FromResult<ImageSource?>(null);
            cache.Add(path, completion.Task);
            return completion.Task;
        }
    }
    private void Run()
    {
        foreach (var (path, completion) in queue.GetConsumingEnumerable())
        {
            try
            {
                var image = read(path);
                image?.Freeze();
                completion.TrySetResult(image);
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("icon-read-failed " + ex.GetType().Name);
                completion.TrySetResult(null);
            }
        }
    }
    public void Dispose() { lock (cache) queue.CompleteAdding(); }
}

public static class ShellIcon
{
    private static readonly Lazy<IconReader> Reader = new(() => new(path => new ShellIconConverter()
        .Convert(path, typeof(ImageSource), null!, CultureInfo.InvariantCulture) as ImageSource));
    private static readonly Lazy<BitmapImage> Placeholder = new(() =>
    {
        var image = new BitmapImage(new Uri("pack://application:,,,/Assets/Fluent/apps.png"));
        image.Freeze();
        return image;
    });
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(string), typeof(ShellIcon), new PropertyMetadata(null, Changed));
    public static string? GetTarget(DependencyObject image) => (string?)image.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject image, string? target) => image.SetValue(TargetProperty, target);
    private static async void Changed(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        if (source is not Image image) return;
        image.Source = Placeholder.Value;
        if (args.NewValue is not string path || path.Length == 0) return;
        var icon = await Reader.Value.Get(path).ConfigureAwait(false);
        // Recycled controls must not receive the result for an earlier item.
        void Apply() { if (GetTarget(image) == path && icon is not null) image.Source = icon; }
        if (image.Dispatcher.HasShutdownStarted) return;
        if (image.Dispatcher.CheckAccess()) Apply();
        else _ = image.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, Apply);
    }
}
