using System.IO;
using System.Text.Json;

namespace IStart;

internal sealed class ActivitySnapshot
{
    public int Version { get; set; } = 1;
    public DateTime? AttemptedSlot { get; set; }
    public DateTimeOffset? LastRefreshedAt { get; set; }
    public List<SoftwareCandidate> Apps { get; set; } = [];
    public Dictionary<string, int> Usage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<LaunchRecord> Files { get; set; } = [];
    public List<LaunchRecord> Folders { get; set; } = [];
    public WebsiteHistoryResult Websites { get; set; } = new([], 0, 0);
}

internal sealed class ActivityCache(string directory)
{
    private readonly string path = Path.Combine(directory, "activity-cache.json");
    private const int MaximumBytes = 16 * 1024 * 1024;

    internal ActivitySnapshot? Load()
    {
        foreach (var file in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length > MaximumBytes) continue;
                var snapshot = JsonSerializer.Deserialize<ActivitySnapshot>(File.ReadAllText(file));
                if (snapshot is not { Version: 1, Apps: not null, Usage: not null, Files: not null, Folders: not null, Websites.Visits: not null }
                    || snapshot.Apps.Count > 8192 || snapshot.Usage.Count > 4096 || snapshot.Files.Count > 1000
                    || snapshot.Folders.Count > 1000 || snapshot.Websites.Visits.Count > 200000
                    || snapshot.Apps.Any(a => a is null || string.IsNullOrWhiteSpace(a.Title) || string.IsNullOrWhiteSpace(a.Shortcut))
                    || snapshot.Files.Concat(snapshot.Folders).Any(r => r is null || string.IsNullOrWhiteSpace(r.Target) || r.Count < 1)
                    || snapshot.Websites.Visits.Any(v => v is null || string.IsNullOrWhiteSpace(v.Url))) continue;
                return snapshot;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return null;
    }

    internal void Save(ActivitySnapshot snapshot)
    {
        snapshot.Websites = WebsiteHistory.ForCache(snapshot.Websites);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        if (bytes.Length > MaximumBytes) throw new IOException("活动缓存超过大小限制");
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal void Clear()
    {
        foreach (var file in new[] { path, path + ".bak" })
            if (File.Exists(file)) File.Delete(file);
    }

    internal static DateTime LatestSlot(DateTime now)
    {
        var noon = now.Date.AddHours(12.5);
        var evening = now.Date.AddHours(18);
        return now >= evening ? evening : now >= noon ? noon : now.Date.AddDays(-1).AddHours(18);
    }
}
