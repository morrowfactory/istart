using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal static partial class SmokeTest
{
    internal static async Task VerifyActivityRefresh(string output)
    {
        var window = new MainWindow(new LibraryStore(Path.Combine(output, "refresh", "library.json")), false);
        try
        {
            var first = window.DiscoverSoftware();
            var second = window.DiscoverSoftware();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(8));
            await window.DiscoverSoftware().WaitAsync(TimeSpan.FromSeconds(8));
            File.AppendAllText(Path.Combine(output, "result.txt"), "\nPASS：失效活动不挤占软件名额、来源失败隔离、文件夹搜索与重复项；重复软件刷新合并并复用近期结果。\n");
        }
        finally { window.Close(); }
    }

    private static void VerifyActivityResilience(string fixture)
    {
        var portable = Path.Combine(fixture, "PortableCad.exe");
        var evidence = Enumerable.Range(0, 140).ToDictionary(i => Path.Combine(fixture, $"removed-{i}.exe"), _ => 500,
            StringComparer.OrdinalIgnoreCase);
        evidence[portable] = 1;
        Check(SoftwareDiscovery.Read(CancellationToken.None, [], evidence).Single().Executable == portable,
            "失效旧程序记录不占据有效便携软件的补充名额");

        var document = Path.Combine(fixture, "中文 文档.txt");
        var record = new LaunchRecord(document, "文档", DateTimeOffset.UtcNow, 1);
        var failures = 0;
        var history = RecentDocuments.ReadSources(CancellationToken.None,
            [() => throw new IOException("locked source"), () => [record],
             () => throw new UnauthorizedAccessException(), () => [record],
             () => throw new COMException(), () => throw new System.Xml.XmlException()], () => failures++);
        Check(failures == 4 && RecentDocuments.Merge([], history).Single() == record,
            "锁定、拒绝访问、跳转列表和 XML 来源失败互相隔离，保留已读文件并去重");
        Check(RecentDocuments.Read(CancellationToken.None, Path.Combine(fixture, "absent-recent")).Count == 0,
            "不存在的 Windows Recent 测试目录可安全读取");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { RecentDocuments.ReadSources(cancelled.Token, [() => [record]]); throw new InvalidOperationException("应取消读取"); }
        catch (OperationCanceledException) { }

        var folders = new List<LaunchRecord>();
        for (var i = 0; i < 4; i++)
        {
            var path = Path.Combine(fixture, "folder-search-" + i);
            Directory.CreateDirectory(path);
            folders.Add(new(path, "目录", DateTimeOffset.UtcNow.AddMinutes(-i), 10 - i));
        }
        var match = folders[^1];
        Check(FrequentFolders.Build([], [], folders, 2, Guid.Empty, new HashSet<string>())
            .All(e => e.Target != match.Target), "低频文件夹位于未搜索视图的容量之外");
        foreach (var recent in new[] { false, true })
            Check(FrequentFolders.Build([], [], folders, 2, Guid.Empty, new HashSet<string>(), recent, "folder-search-3")
                .Single().Target == match.Target, "文件夹搜索先筛选后限额，常用和最近排序均可找到后排目录");
        var pinned = new MenuEntry(Guid.NewGuid(), match.Target, "研发档案", Guid.Empty);
        var duplicate = pinned with { Id = Guid.NewGuid(), Target = match.Target.ToUpperInvariant() + "\\" };
        Check(FrequentFolders.Build([pinned, duplicate], [], [], 1, Guid.Empty, new HashSet<string>(), query: "研发")
            .Single() == pinned, "固定文件夹按自定义名称搜索，并兼容大小写与尾斜杠重复项");
    }
}
