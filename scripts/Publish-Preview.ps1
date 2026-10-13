param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'artifacts\preview-win-x64'
& $Dotnet publish (Join-Path $projectRoot 'src\IStart.App\IStart.App.csproj') -c Release -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw '验证版打包失败' }
$zip = Join-Path $projectRoot 'artifacts\iStart-preview-win-x64.zip'
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force
Get-FileHash $zip -Algorithm SHA256
Write-Output '仅供技术验证，不接管开始按钮或 Win 键，不是正式安装包。'
