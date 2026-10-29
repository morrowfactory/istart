param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ReleaseSha,
    [Parameter(Mandatory)][ValidatePattern('^\d+$')][string]$BuildRunId,
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ApprovalNote
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repo = $env:GITHUB_REPOSITORY
if ($repo -ne 'morrowfactory/istart') { throw '发布目标仓库不符合批准范围' }
function Get-GitHubJson([string]$Path) { gh api $Path | ConvertFrom-Json }
function Assert-LatestMain {
    if ((Get-GitHubJson "repos/$repo/commits/main").sha -ne $ReleaseSha) { throw 'main 已变化，停止发布' }
}
Assert-LatestMain
if ((git rev-parse HEAD) -ne $ReleaseSha) { throw '检出提交与发布提交不同' }
$run = Get-GitHubJson "repos/$repo/actions/runs/$BuildRunId"
$workflow = Get-GitHubJson "repos/$repo/actions/workflows/build.yml"
if ($run.head_sha -ne $ReleaseSha -or $run.head_branch -ne 'main' -or $run.event -ne 'push' -or $run.conclusion -ne 'success' -or $run.workflow_id -ne $workflow.id) { throw '构建来源校验失败' }
$root = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content (Join-Path $root 'src/IStart.App/IStart.App.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+-preview\.\d+$') { throw '此流程只发布明确的预览版本' }
$tag = "v$version"
$name = "iStart-Setup-$version.exe"
$directory = (Resolve-Path -LiteralPath $ArtifactDirectory).Path
$files = @(Get-ChildItem -LiteralPath $directory -File -Recurse)
if ($files.Count -ne 2 -or @($files | Where-Object { $_.Name -notin @($name, "$name.sha256") -or $_.DirectoryName -ne $directory }).Count -gt 0) { throw '产物文件不在安装包允许清单内' }
$package = Get-Item -LiteralPath (Join-Path $directory $name)
$hashFile = Join-Path $directory "$name.sha256"
$parts = (Get-Content -LiteralPath $hashFile -Raw).Trim() -split '\s+', 2
$hash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($parts.Count -ne 2 -or $parts[0].ToLowerInvariant() -ne $hash -or $parts[1] -ne $name) { throw '安装包 SHA256 不一致' }
$releases = @(Get-GitHubJson "repos/$repo/releases?per_page=100")
if (@($releases | Where-Object tag_name -eq $tag).Count -gt 0 -or (git ls-remote origin "refs/tags/$tag")) { throw '版本或标签已经存在，禁止覆盖已发布版本' }
$branch = Get-GitHubJson "repos/$repo/branches/main"
$manifest = Join-Path $directory 'release-manifest.json'
@{
    version = $version
    release_sha = $ReleaseSha
    build_run_id = $BuildRunId
    build_run_url = $run.html_url
    installer = $name
    installer_sha256 = $hash
    installer_bytes = $package.Length
    approval_note = $ApprovalNote
    main_protected = $branch.protected
    published_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding utf8
$notes = Join-Path $env:RUNNER_TEMP "istart-$version-notes.md"
@"
Windows 个人开始菜单预览版。本版修复有使用记录的软件和最近文件遗漏，补充 FreeCAD 与 STP/STEP 历史，并改进文件夹搜索、活动刷新和来源容错。

## 更新内容与修复

- 补充没有开始菜单快捷方式、但有 Windows 使用依据的本机程序，包括便携版 FreeCAD；保留已有快捷方式及其启动参数。
- 失效旧版本路径和已发现入口不再占据有效程序发现名额；我的软件仍按本机活动排序，最多显示 30 个，不恢复独立软件推荐区域。
- 最近打开补充 FreeCAD 保存的 STP、STEP、FCStd 等有效本机文件；支持默认配置及版本目录，沿用外部历史开关、去重和最多 100 个文件的限制。
- Windows Recent、跳转列表或某个应用历史读取失败，不再连带丢弃其他成功来源；文件夹读取失败也保留已完成的文件结果，提供中英文部分读取提示。
- 常用与最近两种文件夹模式先搜索再限制结果，支持自定义名称；修复大小写、尾斜杠重复目录导致的异常，不改变原文件夹和收藏数据。
- 切回“我的软件”时刷新活动；修复反复刷新时旧取消状态覆盖新任务的问题，并释放已完成的取消资源。
- 增加 FreeCAD 中文路径、MRU 数字顺序、过滤与损坏配置，失效路径挤占、来源失败、文件夹搜索及反复刷新回归；核心、自包含 EXE、中英文布局、隔离安装/覆盖/卸载由本次 main 构建验证。
- 保留 Windows/Google 两种紧凑风格、跟随系统/中英文切换、Chrome/Edge 合并网站历史、我的电脑、彩色标注截屏、原生菜单恢复和自动/手动升级。
- 安装包 $name 用于 Windows x64 当前账户，包含离线运行环境，无需另装 .NET；升级保留个人入口、语言、隐私、菜单模式与开机启动选择，不重启电脑。

## 范围与已知限制

- FreeCAD MRU 不提供每个文件的精确打开时间；配置修改时间仅作近似排序，不代表精确使用次数。不监控自定义 --user-cfg 位置和尚未保存的会话记录。
- 历史来源有限，无法保证所有应用完整历史；网络、失效、目录和程序目标不进入最近文件。沿用五秒界面读取预算，不保证整体超时时返回部分结果。
- Win10/11 已知版本可在定位成功后主动启用兼容试用；只有 Win11 26200 有基本实机证据，Win10、其他 Win11、多屏及完整异常安装矩阵仍待验收。
- 安装包未签名，本版仍为预览版；卸载保留个人配置，菜单及托盘可恢复 Windows 原生菜单。

来源提交：$ReleaseSha
构建与安装验证：$($run.html_url)
安装包 SHA256：$hash
发布授权：$ApprovalNote
main 分支保护当前为 $($branch.protected)；本次使用会话明确授权，没有声称存在 GitHub 审批保护。

安装包、校验文件和 release-manifest.json 由 GitHub Actions 发布；请保留个人配置和已验证的上一安装包用于回退。
"@ | Set-Content -LiteralPath $notes -Encoding utf8
Assert-LatestMain
gh release create $tag --repo $repo --target $ReleaseSha --title "iStart $version 预览版" --notes-file $notes --prerelease --draft
gh release upload $tag $package.FullName $hashFile $manifest --repo $repo
Assert-LatestMain
gh release edit $tag --repo $repo --draft=false --latest=false
$release = Get-GitHubJson "repos/$repo/releases/tags/$tag"
if ($release.draft -or !$release.prerelease -or (Get-GitHubJson "repos/$repo/commits/$tag").sha -ne $ReleaseSha) { throw '公开发布状态或标签提交验证失败' }
if ($release.assets.Count -ne 3 -or @($release.assets | Where-Object name -notin @($name, "$name.sha256", 'release-manifest.json')).Count -gt 0) { throw '公开资产清单不一致' }
$verify = Join-Path $env:RUNNER_TEMP ('istart-verify-' + [Guid]::NewGuid().ToString('N'))
gh release download $tag --repo $repo --dir $verify
if ((Get-FileHash -LiteralPath (Join-Path $verify $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw '公开下载的安装包校验失败' }
$publicManifest = Get-Content -LiteralPath (Join-Path $verify 'release-manifest.json') -Raw | ConvertFrom-Json
if ($publicManifest.release_sha -ne $ReleaseSha -or $publicManifest.build_run_id -ne $BuildRunId) { throw '公开版本标记不一致' }
Assert-LatestMain
Write-Output "发布与下载验收通过：$($release.html_url)"
