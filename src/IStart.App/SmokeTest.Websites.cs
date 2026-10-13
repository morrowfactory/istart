using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal static partial class SmokeTest
{
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint db, int flags, nint vfs);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(nint db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(nint db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, nint callback, nint context, nint error);
    private static void VerifyWebsiteHistory(string fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var first = Path.Combine(fixture, "Chrome History");
        var second = Path.Combine(fixture, "Edge History");
        void Create(string path, string rows)
        {
            Check(sqlite3_open_v2(path, out var db, 6, 0) == 0, "创建隔离网站历史夹具");
            try { Check(sqlite3_exec(db, "CREATE TABLE urls(id INTEGER PRIMARY KEY,url LONGVARCHAR,title LONGVARCHAR,visit_count INTEGER NOT NULL,typed_count INTEGER NOT NULL DEFAULT 0,last_visit_time INTEGER NOT NULL,hidden INTEGER NOT NULL DEFAULT 0); INSERT INTO urls(url,title,visit_count,last_visit_time,hidden) VALUES " + rows, 0, 0, 0) == 0, "写入隔离网站历史夹具"); }
            finally { sqlite3_close(db); }
        }
        var older = now.AddHours(-2).ToFileTime() / 10;
        var newer = now.ToFileTime() / 10;
        Create(first, $"('https://work.example/a','工作网站',8,{older},0),('https://work.example/b','工作网站',2,{older},0),('https://hidden.example/','隐藏',99,{newer},1)");
        Create(second, $"('https://work.example/c','工作网站',5,{older},0),('https://recent.example/news','最近网站',1,{newer},0),('file:///private.txt','本机文件',99,{newer},0)");
        var history = WebsiteHistory.Read(CancellationToken.None, [first, second]);
        Check(history.ReadProfiles == 2 && history.FailedProfiles == 0 && history.Visits.Count == 4, "双浏览器读取且过滤隐藏记录及本机文件");
        var frequent = WebsiteHistory.Build(history.Visits, [], false, "", new HashSet<string>());
        Check(frequent.Count == 2 && frequent[0].Target == "https://work.example/", "网站按域名合并并以访问次数排序");
        Check(WebsiteHistory.Build(history.Visits, [], true, "", new HashSet<string>())[0].Target == "https://recent.example/", "最近网站按时间排序");
        Check(WebsiteHistory.Build(history.Visits, [], false, "recent", new HashSet<string>()).Single().Target == "https://recent.example/", "网站地址搜索");
        Check(WebsiteHistory.Build(history.Visits, [], false, "", new HashSet<string> { "https://work.example/" }).Count == 1, "网站临时隐藏");
        var bad = Path.Combine(fixture, "unsupported-history");
        File.WriteAllText(bad, "not sqlite");
        Check(WebsiteHistory.Read(CancellationToken.None, [bad]).FailedProfiles == 1, "损坏历史明确报告失败");
    }
}
