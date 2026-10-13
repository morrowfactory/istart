using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

internal static class SmokeTest
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var fixture = Path.Combine(output, "fixture");
        Directory.CreateDirectory(fixture);
        var file = Path.Combine(fixture, "中文 文档.txt");
        var second = Path.Combine(fixture, "第二项.txt");
        File.WriteAllText(file, "source must remain intact");
        File.WriteAllText(second, "second");
        var config = Path.Combine(fixture, "config", "library.json");
        var store = new LibraryStore(config);
        Check(store.AddPaths([file, second, fixture], Guid.Empty) == 3, "文件和文件夹添加");
        Check(store.AddPaths([file.ToUpperInvariant()], Guid.Empty) == 0, "重复项去重");
        var category = Guid.NewGuid();
        var another = Guid.NewGuid();
        store.Change(s => { s.Categories.Add(new(category, "工作")); s.Categories.Add(new(another, "个人")); });
        var entry = store.Library.Entries[1];
        store.Move(entry.Id, category, store.Library.Entries[0].Id);
        store.MoveCategory(another, category);
        var loaded = new LibraryStore(config);
        Check(loaded.Library.Entries[0].Id == entry.Id && loaded.Library.Entries[0].CategoryId == category, "顺序与分类持久化");
        Check(loaded.Library.Categories[1].Id == another, "分类排序持久化");
        loaded.Change(s => s.Entries.RemoveAll(e => e.Id == entry.Id));
        Check(File.ReadAllText(second) == "second", "移除不删除源文件");
        File.WriteAllText(config, "invalid json");
        Check(new LibraryStore(config).Recovered, "损坏配置从备份恢复");
        File.WriteAllText(config, "{\"SchemaVersion\":99}");
        try { _ = new LibraryStore(config); throw new Exception("不应覆盖未知配置版本"); }
        catch (NotSupportedException) { }

        // Persistence failure must leave the in-memory state unchanged.
        var blocked = Path.Combine(fixture, "blocked");
        File.WriteAllText(blocked, "not a directory");
        var failed = new LibraryStore(Path.Combine(blocked, "library.json"));
        try { failed.AddPaths([file], Guid.Empty); throw new Exception("预期保存失败"); }
        catch (IOException) { Check(failed.Library.Entries.Count == 0, "保存失败回退内存状态"); }

        VerifySoftwareDiscovery(fixture);
        store.Change(s => { s.RecommendationsEnabled = false; s.ExcludedRecommendations.Add("excluded-shortcut"); });
        var preferences = new LibraryStore(config);
        Check(!preferences.Library.RecommendationsEnabled && preferences.Library.ExcludedRecommendations.Contains("excluded-shortcut"), "推荐偏好持久化");

        var window = new MainWindow(store);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(712, 604));
        content.Arrange(new Rect(0, 0, 712, 604));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(740, 660, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var context = backdrop.RenderOpen()) context.DrawRectangle(new SolidColorBrush(Color.FromRgb(250, 252, 255)), null, new Rect(0, 0, 740, 660));
        bitmap.Render(backdrop);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, "menu.png"))) encoder.Save(stream);
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 添加、去重、分类排序、持久化、源文件保留、配置恢复、未知版本保护、写入失败回退、软件快捷方式发现与过滤、推荐偏好持久化、WPF 布局渲染。\n真实鼠标拖放与开始菜单接入尚需人工验证。");
        window.Close();
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
    }
    private static void VerifySoftwareDiscovery(string fixture)
    {
        var folder = Path.Combine(fixture, "shortcuts");
        Directory.CreateDirectory(folder);
        var type = Type.GetTypeFromProgID("WScript.Shell")!;
        var shell = Activator.CreateInstance(type)!;
        try
        {
            foreach (var name in new[] { "示例软件", "重复软件", "卸载工具", "失效软件", "文档" })
            {
                var link = ((dynamic)shell).CreateShortcut(Path.Combine(folder, name + ".lnk"));
                try
                {
                    link.TargetPath = name == "失效软件" ? Path.Combine(fixture, "missing.exe")
                        : name == "文档" ? Path.Combine(fixture, "中文 文档.txt") : Environment.ProcessPath!;
                    link.Save();
                }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); }
            }
        }
        finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        var result = SoftwareDiscovery.Read(CancellationToken.None, [folder]);
        Check(result.Count == 1 && File.Exists(result[0].Shortcut), "软件发现过滤与按目标去重");
    }
}
