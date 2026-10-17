using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IStart;

internal sealed record AppRelease(string Version, string Notes, Uri Installer, long Bytes, string Sha256);
internal sealed record DownloadProgress(long Received, long Total);

internal static class GitHubUpdater
{
    internal const string Repository = "https://github.com/morrowfactory/istart";
    internal static string CurrentVersion => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    private const long MaxInstaller = 256L * 1024 * 1024;
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("iStart-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    internal static (Version Core, int Preview)? ParseVersion(string value)
    {
        var match = Regex.Match(value, @"^v?(\d+\.\d+\.\d+)(?:-preview\.(\d+))?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var core)) return null;
        if (!match.Groups[2].Success) return (core, int.MaxValue);
        return int.TryParse(match.Groups[2].Value, out var preview) ? (core, preview) : null;
    }

    internal static string DisplayNotes(string notes)
    {
        var end = notes.IndexOf("来源提交：", StringComparison.Ordinal);
        if (end >= 0) notes = notes[..end];
        notes = notes.Trim();
        return notes.Length == 0 ? L.T("此版本暂未提供更新说明。") : notes.Length > 12000 ? notes[..12000] : notes;
    }

    internal static AppRelease? SelectRelease(string json, string current)
    {
        try { return ParseReleases(json, current); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException(L.T("GitHub 版本信息格式无法识别，请稍后重试。"), ex); }
    }

    private static AppRelease? ParseReleases(string json, string current)
    {
        var installed = ParseVersion(current) ?? throw new InvalidDataException(L.T("当前版本无法识别。"));
        using var document = JsonDocument.Parse(json);
        var releases = new List<AppRelease>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var version = ParseVersion(tag);
            if (version is null || version.Value.CompareTo(installed) <= 0) continue;
            // 正式版本用户只跟随正式版；预览版用户可以升级到更高预览版或正式版。
            if (installed.Preview == int.MaxValue && (release.GetProperty("prerelease").GetBoolean() || version.Value.Preview != int.MaxValue)) continue;
            var cleanVersion = tag.TrimStart('v');
            var name = $"iStart-Setup-{cleanVersion}.exe";
            var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
            if (assets.Length != 1) continue;
            var asset = assets[0];
            var size = asset.GetProperty("size").GetInt64();
            var digest = asset.TryGetProperty("digest", out var field) ? field.GetString() ?? "" : "";
            if (size <= 0 || size > MaxInstaller || !Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) continue;
            if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri)
                || uri.AbsoluteUri != $"{Repository}/releases/download/{tag}/{name}") continue;
            releases.Add(new(cleanVersion, release.TryGetProperty("body", out var notes) ? notes.GetString() ?? "" : "", uri, size, digest[7..].ToLowerInvariant()));
        }
        return releases.OrderByDescending(r => ParseVersion(r.Version)!.Value).FirstOrDefault();
    }

    internal static async Task<AppRelease?> Check(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await Client.GetAsync("https://api.github.com/repos/morrowfactory/istart/releases?per_page=100", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (data.Length + read > 2 * 1024 * 1024) throw new InvalidDataException(L.T("版本信息超出读取上限。"));
            data.Write(buffer, 0, read);
        }
        return SelectRelease(System.Text.Encoding.UTF8.GetString(data.ToArray()), CurrentVersion);
    }

    internal static async Task<string> Download(AppRelease release, string directory, IProgress<DownloadProgress> progress, CancellationToken token, HttpClient? client = null)
    {
        if (ParseVersion(release.Version) is null || release.Bytes <= 0 || release.Bytes > MaxInstaller
            || !Regex.IsMatch(release.Sha256, "^[a-fA-F0-9]{64}$")
            || release.Installer.AbsoluteUri != $"{Repository}/releases/download/v{release.Version}/iStart-Setup-{release.Version}.exe")
            throw new InvalidDataException(L.T("安装包来源不符合此仓库的发布规则。"));
        // 每次下载独立文件夹；失败或取消只删除本次文件，保留已验证安装包。
        Directory.CreateDirectory(directory);
        var session = Path.Combine(directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(session);
        var partial = Path.Combine(session, "installer.part");
        var destination = Path.Combine(session, $"iStart-Setup-{release.Version}.exe");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMinutes(15));
            using var response = await (client ?? Client).GetAsync(release.Installer, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != release.Bytes) throw new InvalidDataException(L.T("安装包大小不一致。"));
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    received += read;
                    if (received > release.Bytes || received > MaxInstaller) throw new InvalidDataException(L.T("安装包超出预期大小。"));
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    progress.Report(new(received, release.Bytes));
                }
                if (received != release.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("安装包校验失败，请重新下载。"));
            }
            token.ThrowIfCancellationRequested();
            File.Move(partial, destination);
            try { Cleanup(directory, session); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return destination;
        }
        catch
        {
            try { if (File.Exists(partial)) File.Delete(partial); Directory.Delete(session); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static async Task VerifyInstaller(string path, AppRelease release, CancellationToken token)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        if (input.Length != release.Bytes || !Convert.ToHexString(await SHA256.HashDataAsync(input, token)).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(L.T("安装包校验失败，请重新下载。"));
    }

    private static void Cleanup(string directory, string current)
    {
        // 只清理更新器拥有的 GUID 子目录及已知文件，至少保留最近两次下载。
        var sessions = new DirectoryInfo(directory).EnumerateDirectories().Where(d =>
            Guid.TryParseExact(d.Name, "N", out _) && !d.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .OrderByDescending(d => d.LastWriteTimeUtc).ToList();
        foreach (var folder in sessions.Skip(2).Where(d => d.FullName != current && d.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7)))
        {
            try
            {
                var files = folder.GetFiles();
                if (folder.GetDirectories().Length != 0 || files.Any(f => f.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || f.Name != "installer.part" && !Regex.IsMatch(f.Name, @"^iStart-Setup-\d+\.\d+\.\d+(?:-preview\.\d+)?\.exe$"))) continue;
                foreach (var file in files) file.Delete();
                folder.Delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
