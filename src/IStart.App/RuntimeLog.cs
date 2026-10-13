using System.IO;

namespace IStart;

internal static class RuntimeLog
{
    private static readonly object gate = new();
    internal static string? DirectoryPath { get; set; }
    internal static void Write(string message)
    {
        if (DirectoryPath is not string directory) return;
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "runtime.log");
                if (File.Exists(path) && new FileInfo(path).Length > 65536)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} pid={Environment.ProcessId} {message}\n");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
