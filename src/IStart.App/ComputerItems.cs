using System.IO;
using System.Runtime.InteropServices;

namespace IStart;

internal sealed record ComputerItem(string Title, string Target, string Detail = "", double? UsedPercent = null);
internal sealed record ComputerSnapshot(List<ComputerItem> Drives, List<ComputerItem> ControlPanel, bool PanelUnavailable);

internal static class ComputerItems
{
    internal const string RecycleBin = "shell:RecycleBinFolder";
    internal const string Network = "shell:NetworkPlacesFolder";
    internal const string ControlPanel = "shell:::{26EE0668-A00A-44D7-9371-BEB064C98683}\\0";

    public static ComputerSnapshot Read(CancellationToken token)
    {
        var drives = new List<ComputerItem>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            token.ThrowIfCancellationRequested();
            var item = new ComputerItem(drive.Name, drive.Name, "设备未就绪");
            try
            {
                // 不查询映射网络盘容量，避免菜单等待网络连接。
                if (drive.DriveType == DriveType.Network) item = item with { Detail = "网络驱动器" };
                else if (drive.IsReady)
                {
                    var total = drive.TotalSize;
                    var free = drive.AvailableFreeSpace;
                    item = item with { Title = $"{(string.IsNullOrEmpty(drive.VolumeLabel) ? "本地磁盘" : drive.VolumeLabel)} ({drive.Name.TrimEnd('\\')})",
                        Detail = $"可用 {Size(free)} / 共 {Size(total)}", UsedPercent = total > 0 ? 100d * (total - free) / total : null };
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            drives.Add(item);
        }
        var panel = new List<ComputerItem>();
        object? shell = null, folder = null, items = null;
        var unavailable = false;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace(ControlPanel);
            if (folder is null) unavailable = true;
            else
            {
                items = ((dynamic)folder).Items();
                var count = Math.Min((int)((dynamic)items).Count, 128);
                for (var i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    object item = ((dynamic)items).Item(i);
                    try
                    {
                        string path = ((dynamic)item).Path;
                        string name = ((dynamic)item).Name;
                        if (!string.IsNullOrWhiteSpace(name) && path.StartsWith("::{", StringComparison.Ordinal))
                            panel.Add(new(name, "shell:" + path));
                    }
                    finally { Marshal.FinalReleaseComObject(item); }
                }
                unavailable = panel.Count == 0;
            }
        }
        catch (COMException) { unavailable = true; }
        finally
        {
            if (items is not null) Marshal.FinalReleaseComObject(items);
            if (folder is not null) Marshal.FinalReleaseComObject(folder);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
        return new(drives, panel.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList(), unavailable);
    }

    private static string Size(long bytes) => bytes >= 1L << 40 ? $"{bytes / (double)(1L << 40):0.#} TB" : $"{bytes / (double)(1L << 30):0.#} GB";
}
