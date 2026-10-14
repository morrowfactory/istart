using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace IStart;

internal static partial class SmokeTest
{
    private sealed class UpdateResponse(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    private static void VerifyUpdater(string fixture)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("fixture installer — never executed");
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        object Release(string version, bool draft = false, string? url = null) => new
        {
            tag_name = "v" + version, draft, prerelease = version.Contains('-'), body = "测试升级说明",
            assets = new[] { new { name = $"iStart-Setup-{version}.exe", size = bytes.Length, digest = "sha256:" + digest,
                browser_download_url = url ?? $"{GitHubUpdater.Repository}/releases/download/v{version}/iStart-Setup-{version}.exe" } }
        };
        var json = JsonSerializer.Serialize(new[] { Release("0.2.0-preview.9"), Release("0.2.0-preview.18", true), Release("0.2.0-preview.17"), Release("0.2.0-preview.99", url: "https://other.example/a.exe") });
        var selected = GitHubUpdater.SelectRelease(json, "0.2.0-preview.16")!;
        Check(selected.Version == "0.2.0-preview.17", "升级按数值版本比较，排除草稿及外部安装地址");
        Check(GitHubUpdater.SelectRelease(json, "0.2.0-preview.17") is null, "相同版本不重复升级");
        Check(GitHubUpdater.SelectRelease(json, "0.2.0") is null, "正式版不自动进入预览通道");
        Check(GitHubUpdater.ParseVersion("../installer") is null, "拒绝不合法版本及路径");
        var directory = Path.Combine(fixture, "updates");
        using var client = new HttpClient(new UpdateResponse(bytes));
        var progress = new Progress<DownloadProgress>();
        var path = Task.Run(() => GitHubUpdater.Download(selected, directory, progress, CancellationToken.None, client)).GetAwaiter().GetResult();
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "安装包下载并校验后才改为 exe");
        try
        {
            Task.Run(() => GitHubUpdater.Download(selected with { Sha256 = new string('0', 64) }, directory, progress, CancellationToken.None, client)).GetAwaiter().GetResult();
            throw new InvalidOperationException("损坏安装包未被拒绝");
        }
        catch (InvalidDataException) { Check(Directory.GetDirectories(directory).Length == 1 && File.Exists(path), "校验失败清理本次下载，保留已有安装包"); }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            Task.Run(() => GitHubUpdater.Download(selected, directory, progress, cancelled.Token, client)).GetAwaiter().GetResult();
            throw new InvalidOperationException("取消下载未生效");
        }
        catch (OperationCanceledException) { Check(Directory.GetDirectories(directory).Length == 1, "取消下载清理临时文件"); }
    }
}
