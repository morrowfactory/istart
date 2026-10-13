param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot 'src\IStart.App\IStart.App.csproj'
& $Dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '构建失败' }
$output = Join-Path $projectRoot ('artifacts\smoke-' + [Guid]::NewGuid().ToString('N'))
$assembly = Join-Path $projectRoot 'src\IStart.App\bin\Release\net10.0-windows\IStart.App.dll'
& $Dotnet $assembly --smoke-test $output
if ($LASTEXITCODE -ne 0) { throw "验证失败，请查看 $output" }
Get-Content (Join-Path $output 'result.txt')
Write-Output "界面渲染：$output\menu.png"
