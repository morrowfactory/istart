param([string]$Dotnet = 'dotnet', [string]$Compiler = 'ISCC.exe', [switch]$SkipPublish, [switch]$TestInstall)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$SkipPublish) { & (Join-Path $PSScriptRoot 'Publish-Preview.ps1') -Dotnet $Dotnet }
$compilerArgs = @()
if ($TestInstall) { $compilerArgs += '/DTestInstall' }
$compilerArgs += (Join-Path $projectRoot 'installer\iStart.iss')
& $Compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw '安装包构建失败' }
$name = if ($TestInstall) { 'iStart-QA' } else { 'iStart' }
$package = Join-Path $projectRoot "artifacts\setup\$name-Setup-0.2.0-preview.14.exe"
$hash = (Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$package.sha256" "$hash  $(Split-Path $package -Leaf)" -Encoding ascii
Get-Item $package | Select-Object FullName, Length
