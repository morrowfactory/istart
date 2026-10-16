param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'artifacts\preview-win-x64'
if (Test-Path -LiteralPath $output) {
    $resolved = (Resolve-Path -LiteralPath $output).Path
    if (!$resolved.StartsWith($projectRoot + '\artifacts\', [StringComparison]::OrdinalIgnoreCase) -or
        ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw '拒绝清理范围外或重解析的发布目录' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
& $Dotnet publish (Join-Path $projectRoot 'src\IStart.App\IStart.App.csproj') -c Release -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw '验证版打包失败' }
$zip = Join-Path $projectRoot 'artifacts\iStart-preview-win-x64.zip'
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force
Get-FileHash $zip -Algorithm SHA256
Write-Output '试用版：默认保留原生菜单，接入须在设置中启用并通过兼容检查。'
