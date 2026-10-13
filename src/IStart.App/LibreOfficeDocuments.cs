using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace IStart;

internal static class LibreOfficeDocuments
{
    internal static List<LaunchRecord> Read(CancellationToken token, string? path = null)
    {
        path ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibreOffice", "4", "user", "registrymodifications.xcu");
        if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 });
        var xml = XDocument.Load(reader);
        XNamespace oor = "http://openoffice.org/2001/registry";
        var nodes = xml.Root?.Elements("item").Where(item =>
            ((string?)item.Attribute(oor + "path"))?.EndsWith("HistoryInfo['PickList']/OrderList", StringComparison.Ordinal) == true)
            .SelectMany(item => item.Elements("node"))
            .OrderBy(node => int.TryParse((string?)node.Attribute(oor + "name"), out var index) ? index : int.MaxValue).Take(100) ?? [];
        var result = new List<LaunchRecord>();
        foreach (var node in nodes)
        {
            token.ThrowIfCancellationRequested();
            var value = node.Elements("prop").FirstOrDefault(p => (string?)p.Attribute(oor + "name") == "HistoryItemRef")?.Element("value")?.Value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.IsFile || uri.IsUnc) continue;
            if (RecentDocuments.IsDocument(uri.LocalPath))
                result.Add(new(uri.LocalPath, Path.GetFileName(uri.LocalPath), new DateTimeOffset(File.GetLastWriteTimeUtc(path)), 1));
        }
        return result;
    }
}
