using System.IO;
using System.Text.Json;

namespace IStart;

public sealed record MenuEntry(Guid Id, string Target, string Title, Guid CategoryId);
public sealed record MenuCategory(Guid Id, string Name);
public sealed class MenuLibrary
{
    public int SchemaVersion { get; set; } = 1;
    public List<MenuCategory> Categories { get; set; } = [new(Guid.Empty, "未分类")];
    public List<MenuEntry> Entries { get; set; } = [];
    public bool RecommendationsEnabled { get; set; } = true;
    public List<string> ExcludedRecommendations { get; set; } = [];
}

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string path;
    public MenuLibrary Library { get; private set; }
    public bool Recovered { get; }

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
        if (state.SchemaVersion != 1) throw new NotSupportedException("不支持此配置版本");
        if (state.Categories is null || state.Entries is null || state.ExcludedRecommendations is null
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

    public void MoveCategory(Guid id, Guid before)
    {
        if (id == before || id == Guid.Empty || before == Guid.Empty) return;
        Change(state =>
        {
            var category = state.Categories.Single(c => c.Id == id);
            state.Categories.Remove(category);
            state.Categories.Insert(state.Categories.FindIndex(c => c.Id == before), category);
        });
    }
}
