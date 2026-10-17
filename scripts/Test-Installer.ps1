param([string]$Package, [string]$Output)
$ErrorActionPreference = 'Stop'
if (!(Split-Path $Package -Leaf).StartsWith('iStart-QA-Setup-')) { throw '只能测试隔离身份的 QA 安装包' }
$Output = [IO.Path]::GetFullPath($Output)
$root = [IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'))
if (!$Output.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '测试目录必须位于 artifacts 内' }
if (Test-Path $Output) { throw '使用新的测试目录，避免覆盖已有文件' }
New-Item $Output -ItemType Directory | Out-Null
$install = Join-Path $Output '已安装程序 QA'
function Run-Setup([string]$Executable, [string[]]$Arguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "安装器返回 $($process.ExitCode)" }
}
Run-Setup $Package @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=""', "/DIR=`"$install`"", "/LOG=`"$Output\install.log`"")
if (!(Test-Path "$install\IStart.App.exe")) { throw '程序文件未安装' }
Run-Setup $Package @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOCLOSEAPPLICATIONS', '/AUTOMATICUPDATE=1', '/MERGETASKS=!desktopicon', "/DIR=`"$install`"", "/LOG=`"$Output\upgrade.log`"")
Run-Setup "$install\unins000.exe" @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$Output\uninstall.log`"")
if (Test-Path "$install\IStart.App.exe") { throw '卸载遗留程序文件' }
Set-Content "$Output\result.txt" 'PASS：隔离 AppId 首次安装、带自动更新参数覆盖安装、卸载；中文空格路径；QA 安装不启动菜单。未验证生产身份安装后重启、交互取消、空间不足、权限拒绝、运行中文件占用及完整失败回退。' -Encoding utf8
Get-Content "$Output\result.txt"
