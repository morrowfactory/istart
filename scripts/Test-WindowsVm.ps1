param(
    [string]$App = "$env:LOCALAPPDATA\Programs\iStart\IStart.App.exe",
    [string]$Output = "$env:USERPROFILE\Desktop\iStart-test-results",
    [ValidateRange(1, 20)][int]$Rounds = 3,
    [switch]$PreflightOnly
)
$ErrorActionPreference = 'Stop'
$os = Get-CimInstance Win32_OperatingSystem
$windows = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$machine = Get-CimInstance Win32_ComputerSystem
$build = [int]$os.BuildNumber
$isVm = $machine.Model -match 'VirtualBox|Virtual Machine|VMware|KVM|HVM domU'
$session = (Get-Process -Id $PID).SessionId
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$elevated = (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$run = Join-Path $Output ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$state = [ordered]@{
    Started = (Get-Date).ToString('o'); Build = $build; OS = $os.Caption
    Revision = $windows.UBR; Edition = $windows.EditionID; DisplayVersion = $windows.DisplayVersion
    X64 = [Environment]::Is64BitOperatingSystem; VirtualMachine = [bool]$isVm
    Session = $session; Elevated = $elevated
    AppVersion = $null; Status = 'NotRun'; Checks = @()
    Manual = @('连续开关 100 次及空闲 45 分钟后重试', '100/150/200% DPI',
        '任务栏自动隐藏和不同位置', 'Explorer 重启', '锁屏/睡眠恢复',
        '升级/卸载后原生入口恢复', '中文输入法和真实键盘/鼠标')
}
try {
    if (Test-Path -LiteralPath $App -PathType Leaf) {
        $App = (Resolve-Path -LiteralPath $App).Path
        $dll = Join-Path (Split-Path $App) 'IStart.App.dll'
        $state.AppVersion = (Get-Item -LiteralPath $dll).VersionInfo.ProductVersion
    }
    if ($PreflightOnly) {
        $state.Status = 'PreflightOnly'
        return
    }
    if (-not $isVm -or $build -notin @(19044, 19045) -or -not $state.X64) {
        throw '交互测试仅允许在 Win10 21H2/22H2 x64 虚拟机中运行，不能在日常宿主机注入输入。'
    }
    if (-not $state.AppVersion) { throw '请先在虚拟机中安装经过哈希校验的 iStart 安装包。' }
    if (-not [Environment]::UserInteractive -or $session -eq 0 -or $elevated) {
        throw '需要已登录、未锁屏的普通权限交互桌面，不能以管理员或服务身份测试。'
    }
    if (-not (Get-Process explorer -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session })) {
        throw '当前会话没有 Explorer 桌面，停止交互测试。'
    }
    $existing = @(Get-Process -Name IStart.App -ErrorAction SilentlyContinue | Where-Object {
        $_.SessionId -eq $session
    })
    if ($existing.Count) {
        throw '请先从虚拟机中的 iStart 托盘选择退出，再运行测试；脚本不终止日常实例。'
    }
    Write-Host '正在虚拟机中操作 Win 键、开始按钮和测试窗口，请暂时不要使用虚拟机键鼠。'
    $checks = @(
        @{ Mode = '--probe-shell'; Result = 'shell-probe.txt'; Name = '开始按钮发现' },
        @{ Mode = '--smoke-test'; Result = 'result.txt'; Name = '数据、布局与恢复回归' },
        @{ Mode = '--input-test'; Result = 'input-result.txt'; Name = '输入接入与原生恢复' }
    )
    for ($i = 1; $i -le $Rounds; $i++) {
        $checks += @{ Mode = '--presentation-test'; Result = 'presentation-result.txt'; Name = "置前与实际菜单第 $i 轮" }
    }
    foreach ($check in $checks) {
        $dir = Join-Path $run ('case-' + $state.Checks.Count)
        $record = [ordered]@{ Name = $check.Name; Status = 'Fail'; ExitCode = $null }
        $state.Checks += $record
        $info = New-Object System.Diagnostics.ProcessStartInfo
        $info.FileName = $App
        $info.UseShellExecute = $false
        $info.Arguments = $check.Mode + ' "' + $dir + '"'
        $process = [System.Diagnostics.Process]::Start($info)
        try {
            if (-not $process.WaitForExit(180000)) {
                Get-CimInstance Win32_Process -Filter ("ParentProcessId=" + $process.Id) | Where-Object {
                    $_.ExecutablePath -eq $App -and $_.CommandLine -like '*--hook-child*' -and $_.CommandLine.Contains($dir)
                } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
                $process.Kill() # 只结束此处启动的测试进程，Windows 会释放其输入钩子。
                $process.WaitForExit()
                throw ($check.Name + '超时，已停止本轮测试。')
            }
            $record.ExitCode = $process.ExitCode
            $result = Join-Path $dir $check.Result
            if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $result)) {
                throw ($check.Name + '失败，请查看本轮目录中的 failure 文件。')
            }
            $content = Get-Content -LiteralPath $result -Raw -Encoding UTF8
            if ($check.Mode -eq '--probe-shell') {
                if ($content -notmatch '开始按钮数量：[1-9]') { throw '未发现有效开始按钮，停止输入测试。' }
            } elseif ($content -notmatch 'PASS') { throw ($check.Name + '缺少通过结果。') }
            $record.Status = 'Pass'
            Write-Host ($check.Name + '：通过')
        } finally { $process.Dispose() }
    }
    $state.Status = 'AutomatedPass-ManualPending'
} catch {
    $state.Status = 'Fail'
    $state.Error = $_.Exception.Message
    throw
} finally {
    $state.Finished = (Get-Date).ToString('o')
    $state | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding UTF8
    Write-Host ('报告：' + $run)
}
