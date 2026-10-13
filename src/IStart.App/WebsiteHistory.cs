using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal sealed record WebsiteVisit(string Url, string Title, long Count, DateTimeOffset LastOpened);
internal sealed record WebsiteHistoryResult(List<WebsiteVisit> Visits, int ReadProfiles, int FailedProfiles);

internal static class WebsiteHistory
{
    private const string Sqlite = "winsqlite3.dll";
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint db, int flags, nint vfs);
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(nint db);
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(nint db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out nint statement, nint tail);
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(nint statement);
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(nint statement);
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern nint sqlite3_column_text(nint statement, int column);
    [DllImport(Sqlite, CallingConvention = CallingConvention.Cdecl)] private static extern long sqlite3_column_int64(nint statement, int column);

    internal static WebsiteHistoryResult Read(CancellationToken token, IEnumerable<string>? databases = null)
    {
        var result = new List<WebsiteVisit>();
        var read = 0;
        var failed = 0;
        foreach (var path in databases ?? Profiles())
        {
            token.ThrowIfCancellationRequested();
            try { result.AddRange(ReadDatabase(path, token)); read++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException) { failed++; }
        }
        return new(result, read, failed);
    }

    private static IEnumerable<string> Profiles()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var browser in new[] { "Google/Chrome/User Data", "Microsoft/Edge/User Data" })
        {
            var root = Path.Combine(local, browser);
            if (!Directory.Exists(root)) continue;
            foreach (var folder in new DirectoryInfo(root).EnumerateDirectories().Where(d => d.Name == "Default" || d.Name.StartsWith("Profile ", StringComparison.Ordinal)).Take(10))
            {
                var path = Path.Combine(folder.FullName, "History");
                if (File.Exists(path)) yield return path;
            }
        }
    }

    private static List<WebsiteVisit> ReadDatabase(string source, CancellationToken token)
    {
        var info = new FileInfo(source);
        if (info.Length > 128 * 1024 * 1024) throw new IOException("浏览器历史过大，本次跳过");
        var folder = Path.Combine(Path.GetTempPath(), "iStart-sites-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        nint db = 0, statement = 0;
        try
        {
            var snapshot = Path.Combine(folder, "History");
            // Browser databases may be locked; read only a private, short-lived local snapshot.
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(source + suffix))
                {
                    if (new FileInfo(source + suffix).Length > 128 * 1024 * 1024) throw new IOException("历史辅助文件过大");
                    File.Copy(source + suffix, snapshot + suffix);
                }
            }
            if (sqlite3_open_v2(snapshot, out db, 1, 0) != 0) throw new IOException("无法打开历史快照");
            var sql = "SELECT url,title,visit_count,last_visit_time FROM urls WHERE hidden=0 AND visit_count>0 AND (url LIKE 'https://%' OR url LIKE 'http://%') ORDER BY last_visit_time DESC LIMIT 10000";
            if (sqlite3_prepare_v2(db, sql, -1, out statement, 0) != 0) throw new IOException("浏览器历史结构不支持");
            var result = new List<WebsiteVisit>();
            int code;
            while ((code = sqlite3_step(statement)) == 100)
            {
                token.ThrowIfCancellationRequested();
                var url = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 0)) ?? "";
                var title = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 1)) ?? "";
                var count = sqlite3_column_int64(statement, 2);
                var time = sqlite3_column_int64(statement, 3);
                if (time <= 0 || time > DateTime.MaxValue.ToFileTimeUtc() / 10) continue;
                result.Add(new(url, title, count, DateTimeOffset.FromFileTime(time * 10)));
            }
            if (code != 101) throw new IOException("历史快照读取不完整");
            return result;
        }
        finally
        {
            if (statement != 0) sqlite3_finalize(statement);
            if (db != 0) sqlite3_close(db);
            try
            {
                var cleanup = Path.GetFullPath(folder);
                if (Path.GetDirectoryName(cleanup)?.Equals(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) == true
                    && Path.GetFileName(cleanup).StartsWith("iStart-sites-", StringComparison.Ordinal)) Directory.Delete(cleanup, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { RuntimeLog.Write("website-snapshot-cleanup-failed"); }
        }
    }

    internal static List<MenuEntry> Build(IEnumerable<WebsiteVisit> history, IEnumerable<LaunchRecord> own, bool recent, string query, ISet<string> hidden)
    {
        static Uri? Address(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0 ? uri : null;
        var sources = history.Concat(own.Select(r => new WebsiteVisit(r.Target, r.Title, r.Count, r.OpenedAt)));
        var sites = sources.Select(v => (Visit: v, Uri: Address(v.Url))).Where(v => v.Uri is not null)
            .GroupBy(v => v.Uri!.IdnHost + (v.Uri.IsDefaultPort ? "" : ":" + v.Uri.Port), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latest = group.OrderByDescending(v => v.Visit.LastOpened).First();
                var url = latest.Uri!.GetLeftPart(UriPartial.Authority) + "/";
                return (Url: url, Title: string.IsNullOrWhiteSpace(latest.Visit.Title) ? group.Key : latest.Visit.Title,
                    Count: group.Sum(v => Math.Clamp(v.Visit.Count, 0, int.MaxValue)), Latest: latest.Visit.LastOpened);
            }).Where(site => !hidden.Contains(site.Url) && (site.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || site.Url.Contains(query, StringComparison.OrdinalIgnoreCase)));
        var ordered = recent ? sites.OrderByDescending(site => site.Latest).ThenByDescending(site => site.Count)
            : sites.OrderByDescending(site => site.Count).ThenByDescending(site => site.Latest);
        return ordered.Take(30).Select(site => new MenuEntry(Guid.NewGuid(), site.Url, site.Title, Guid.Empty)).ToList();
    }
}
