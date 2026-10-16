using System.IO;
using System.Text.Json;

namespace IStart;

public sealed record MenuEntry(Guid Id, string Target, string Title, Guid CategoryId);
public sealed record MenuCategory(Guid Id, string Name);
public sealed record LaunchRecord(string Target, string Title, DateTimeOffset OpenedAt, int Count);
public sealed class MenuLibrary
{
    public int SchemaVersion { get; set; } = 2;
    public List<MenuCategory> Categories { get; set; } = [new(Guid.Empty, "未分类")];
    public List<MenuEntry> Entries { get; set; } = [];
    public List<string> HiddenSoftware { get; set; } = [];
    public List<LaunchRecord> Launches { get; set; } = [];
    public bool RecordLaunches { get; set; } = true;
    public bool ShowWindowsRecentDocuments { get; set; } = true;
    public bool ShowBrowserHistory { get; set; } = true;
    public bool WebsitesRecentFirst { get; set; }
    public bool FoldersRecentFirst { get; set; }
    public bool CustomMenuEnabled { get; set; }
    public bool StartWithWindows { get; set; }
    public string ScreenshotShortcut { get; set; } = "Alt+Shift+A";
    public bool IconView { get; set; }
    public string Theme { get; set; } = "Google";
    public string Language { get; set; } = "System";
}

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string path;
    public MenuLibrary Library { get; private set; }
    public bool Recovered { get; }
    public string DirectoryPath => Path.GetDirectoryName(path)!;

    public LibraryStore(string path)
    {
        this.path = path;
        if (!File.Exists(path)) { Library = new(); return; }
        try { Library = Read(path); }
        catch (JsonException)
        {
            // A newer schema must never be silently replaced with an older backup.
            Library = Read(path + ".bak");
            Recovered = true;
        }
    }

    private static MenuLibrary Read(string file)
    {
        var state = JsonSerializer.Deserialize<MenuLibrary>(File.ReadAllText(file), JsonOptions) ?? throw new JsonException("配置为空");
        if (state.SchemaVersion is not (1 or 2)) throw new NotSupportedException("不支持此配置版本");
        state.SchemaVersion = 2;
        if (state.Theme is not ("Windows" or "Google")) state.Theme = "Google";
        state.Language = L.Normalize(state.Language);
        if (state.Categories is null || state.Entries is null || state.HiddenSoftware is null || state.HiddenSoftware.Count > 256 || state.HiddenSoftware.Any(string.IsNullOrWhiteSpace) || state.Launches is null
            || state.Launches.Any(l => l is null || string.IsNullOrWhiteSpace(l.Target) || string.IsNullOrWhiteSpace(l.Title) || l.Count < 1)
            || state.Launches.Count > 100 || state.Launches.Select(l => l.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Launches.Count
            || state.Categories.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name))
            || state.Entries.Any(e => e is null)
            || !state.Categories.Any(c => c.Id == Guid.Empty)
            || state.Categories.Select(c => c.Id).Distinct().Count() != state.Categories.Count
            || state.Entries.Select(e => e.Id).Distinct().Count() != state.Entries.Count
            || state.Entries.Any(e => e is null || string.IsNullOrWhiteSpace(e.Target) || string.IsNullOrWhiteSpace(e.Title) || !state.Categories.Any(c => c.Id == e.CategoryId)))
            throw new JsonException("配置结构无效");
        return state;
    }

    public void Change(Action<MenuLibrary> edit)
    {
        var next = JsonSerializer.Deserialize<MenuLibrary>(JsonSerializer.Serialize(Library, JsonOptions), JsonOptions)!;
        edit(next);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, next, JsonOptions);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            Library = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public int AddPaths(IEnumerable<string> paths, Guid categoryId, Guid? before = null)
    {
        var added = 0;
        Change(state =>
        {
            foreach (var raw in paths)
            {
                var target = Path.GetFullPath(raw);
                if ((!File.Exists(target) && !Directory.Exists(target)) || state.Entries.Any(e => string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase))) continue;
                var title = Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrEmpty(title)) title = target;
                var index = before is null ? -1 : state.Entries.FindIndex(e => e.Id == before);
                var entry = new MenuEntry(Guid.NewGuid(), target, title, categoryId);
                if (index < 0) state.Entries.Add(entry); else state.Entries.Insert(index, entry);
                added++;
            }
        });
        return added;
    }

    public void Move(Guid entryId, Guid categoryId, Guid? before)
    {
        if (entryId == before) return;
        Change(state =>
        {
            var entry = state.Entries.Single(e => e.Id == entryId);
            state.Entries.Remove(entry);
            var index = before is null ? -1 : state.Entries.FindIndex(e => e.Id == before);
            entry = entry with { CategoryId = categoryId };
            if (index < 0) state.Entries.Add(entry); else state.Entries.Insert(index, entry);
        });
    }

    public void MoveCategory(Guid id, Guid? before)
    {
        if (id == before || id == Guid.Empty || before == Guid.Empty) return;
        Change(state =>
        {
            var category = state.Categories.Single(c => c.Id == id);
            state.Categories.Remove(category);
            if (before is null) state.Categories.Add(category);
            else state.Categories.Insert(state.Categories.FindIndex(c => c.Id == before), category);
        });
    }
    public void RecordLaunch(string target, string title)
    {
        if (!Library.RecordLaunches) return;
        Change(state =>
        {
            var prior = state.Launches.FirstOrDefault(l => string.Equals(l.Target, target, StringComparison.OrdinalIgnoreCase));
            state.Launches.RemoveAll(l => string.Equals(l.Target, target, StringComparison.OrdinalIgnoreCase));
            state.Launches.Insert(0, new(target, title, DateTimeOffset.UtcNow, Math.Min(prior?.Count ?? 0, int.MaxValue - 1) + 1));
            if (state.Launches.Count > 100) state.Launches.RemoveRange(100, state.Launches.Count - 100);
        });
    }
    public void RenameCategory(Guid id, string name) => Change(state =>
    {
        var index = state.Categories.FindIndex(c => c.Id == id);
        state.Categories[index] = state.Categories[index] with { Name = name };
    });
    public void DeleteCategory(Guid id)
    {
        if (id == Guid.Empty) return;
        Change(state =>
        {
            state.Categories.RemoveAll(c => c.Id == id);
            for (var i = 0; i < state.Entries.Count; i++)
                if (state.Entries[i].CategoryId == id) state.Entries[i] = state.Entries[i] with { CategoryId = Guid.Empty };
        });
    }
}
