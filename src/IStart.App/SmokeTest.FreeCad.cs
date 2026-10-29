using System.IO;
using System.Xml.Linq;

namespace IStart;

internal static partial class SmokeTest
{
    private static void VerifyFreeCadDocuments(string fixture)
    {
        var root = Path.Combine(fixture, "FreeCAD");
        var version = Path.Combine(root, "v1-1");
        Directory.CreateDirectory(version);
        var stp = Path.Combine(fixture, "中文 & 装配.stp");
        var step = Path.Combine(fixture, "第二项.STEP");
        var model = Path.Combine(fixture, "模型.FCStd");
        foreach (var path in new[] { stp, step, model }) File.WriteAllText(path, "unchanged model fixture");
        var config = Path.Combine(version, "user.cfg");
        var history = new XElement("FCParamGroup", new XAttribute("Name", "RecentFiles"),
            new XElement("FCInt", new XAttribute("Name", "RecentFiles"), new XAttribute("Value", "4")));
        // Deliberately shuffled: numeric MRU order must beat XML order (including MRU10).
        var paths = new Dictionary<int, string> { [10] = model, [2] = step, [0] = stp, [3] = stp.ToUpperInvariant(),
            [4] = fixture, [5] = Path.Combine(fixture, "missing.stp"), [6] = "relative.stp",
            [7] = @"\\invalid-host\share\network.stp", [8] = Environment.ProcessPath!, [9] = new Uri(stp).AbsoluteUri };
        foreach (var item in paths)
            history.Add(new XElement("FCText", new XAttribute("Name", "MRU" + item.Key), item.Value));
        history.Add(new XElement("FCText", new XAttribute("Name", "MRUwrong"), model));
        var xml = new XDocument(new XElement("FCParameters", new XElement("FCParamGroup", new XAttribute("Name", "Root"),
            new XElement("FCParamGroup", new XAttribute("Name", "BaseApp"),
                new XElement("FCParamGroup", new XAttribute("Name", "Preferences"), history)),
            new XElement("FCParamGroup", new XAttribute("Name", "Material"),
                new XElement("FCParamGroup", new XAttribute("Name", "Recent"),
                    new XElement("FCText", new XAttribute("Name", "MRU0"), Path.Combine(fixture, "中文 文档.txt")))))));
        xml.Save(config);
        var before = File.ReadAllBytes(config);
        var records = FreeCadDocuments.Read(CancellationToken.None, root);
        Check(records.Select(r => r.Target).SequenceEqual([stp, step, model]),
            "FreeCAD 数字 MRU 排序、中文路径、STEP/原生文件与去重，排除目录、失效、网络、程序及无关分组");
        Check(records.All(r => r.Count == 1 && r.OpenedAt == new DateTimeOffset(File.GetLastWriteTimeUtc(config)))
            && File.ReadAllBytes(config).SequenceEqual(before), "FreeCAD 只读，不将 MRU 顺序伪造为精确时间或频率");
        xml.Save(Path.Combine(root, "user.cfg"));
        Check(FreeCadDocuments.Read(CancellationToken.None, root).Count == 3, "FreeCAD 旧版与版本目录配置合并去重");
        File.WriteAllText(config, "<broken>");
        Check(FreeCadDocuments.Read(CancellationToken.None, root).Count == 3, "FreeCAD 单配置损坏不影响其他配置");
        File.WriteAllText(Path.Combine(root, "user.cfg"), "<!DOCTYPE x [<!ENTITY secret SYSTEM 'file:///invalid'>]><x>&secret;</x>");
        Check(FreeCadDocuments.Read(CancellationToken.None, root).Count == 0, "FreeCAD 禁用 DTD 与外部实体");
        using (var large = File.Create(config)) large.SetLength(4 * 1024 * 1024 + 1);
        Check(FreeCadDocuments.Read(CancellationToken.None, root).Count == 0, "FreeCAD 拒绝超限配置");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { FreeCadDocuments.Read(cancelled.Token, root); throw new InvalidOperationException("应取消读取"); }
        catch (OperationCanceledException) { }
    }
}
