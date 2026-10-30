param([string]$Dotnet = 'dotnet', [string]$Output)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputPath = [IO.Path]::GetFullPath($Output)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if (!$outputPath.StartsWith($artifactRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '输出必须位于本项目 artifacts 内' }
if (Test-Path -LiteralPath $outputPath) { throw '使用新的测试目录，避免覆盖已有证据' }
$dotnetPath = (Get-Command $Dotnet -ErrorAction Stop).Source
$assemblyPath = Join-Path $projectRoot 'src\IStart.App\bin\Release\net10.0-windows\IStart.App.dll'
if (!(Test-Path -LiteralPath $assemblyPath)) { throw '请先完成 Release 构建' }
New-Item -ItemType Directory -Path $outputPath | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class IStartWindowTest {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Startup {
        public int Size; public string Reserved, Desktop, Title;
        public uint X, Y, Width, Height, Columns, Rows, Fill, Flags;
        public ushort Show, ReservedSize; public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr Process, Thread; public uint Pid, Tid; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcess(string app, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string cwd, ref Startup startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static void Run(string dotnet, string assembly, string output, string cwd) {
        var name = "iStart-QA-" + Guid.NewGuid().ToString("N");
        // No SwitchDesktop API: this test never changes the user's visible desktop.
        var desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x01ff, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception();
        var info = new ProcessInfo();
        try {
            var startup = new Startup { Size = Marshal.SizeOf<Startup>(), Desktop = "WinSta0\\" + name, Flags = 0x80 };
            var command = new StringBuilder("\"" + dotnet + "\" \"" + assembly + "\" --window-reliability-test \"" + output + "\"");
            if (!CreateProcess(dotnet, command, IntPtr.Zero, IntPtr.Zero, false, 0x08000000, IntPtr.Zero, cwd, ref startup, out info)) throw new Win32Exception();
            if (WaitForSingleObject(info.Process, 45000) != 0) {
                TerminateProcess(info.Process, 2); WaitForSingleObject(info.Process, 5000);
                throw new TimeoutException("隔离窗口检查超时；仅结束本次测试子进程");
            }
            uint code;
            if (!GetExitCodeProcess(info.Process, out code) || code != 0) throw new Exception("隔离窗口检查失败：" + code);
        } finally {
            if (info.Thread != IntPtr.Zero) CloseHandle(info.Thread);
            if (info.Process != IntPtr.Zero) CloseHandle(info.Process);
            CloseDesktop(desktop);
        }
    }
}
'@
[IStartWindowTest]::Run($dotnetPath, $assemblyPath, $outputPath, $projectRoot)
Get-Content -LiteralPath (Join-Path $outputPath 'window-result.txt')
