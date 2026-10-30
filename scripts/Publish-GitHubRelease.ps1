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
Windows 个人开始菜单预览版。本版优化菜单响应、图标后台加载与窗口恢复，并取消焦点重试中的模拟 Alt/Ctrl。

## 更新内容与修复

- 软件、文件和系统入口先显示占位图，由两个后台 STA 线程读取真实图标；相同路径共用请求，队列和缓存有上限，缓慢图标读取不再直接阻塞界面布局。
- 合并重复软件扫描，成功结果复用 30 秒；等待超时后不累积仍未完成的原生读取。软件分类在搜索间缓存，发现完成后更新。
- 菜单恢复使用逻辑尺寸、最小尺寸及 DPI，避免沿用最小化矩形；开关判断使用实际前台窗口归属。
- 焦点重试改为有界直接请求，取消原有模拟 Alt/Ctrl 后备；内容呈现后补充聚焦，记录显示、排队耗时及焦点错误状态，不记录输入文字。
- 新增慢图标、失败恢复、重复请求、队列限额、异步图像替换、扫描复用及多种 DPI 尺寸检查，接入 GitHub 构建；独立非活动桌面已完成双主题共 100 次显示/隐藏，包含 10 次最小化恢复。
- 本次 main 构建验证核心数据、自包含 EXE、中英文双主题、截图、升级夹具及隔离首次安装、覆盖安装和卸载。
- 保留 preview.25 的便携版 FreeCAD 软件发现、STP/STEP 等最近文件、历史来源容错及文件夹搜索改进。
- 保留 Windows/Google 两种紧凑风格、跟随系统/中英文切换、Chrome/Edge 合并网站历史、我的电脑、彩色标注截屏、原生菜单恢复和自动/手动升级。
- 安装包 $name 用于 Windows x64 当前账户，包含离线运行环境，无需另装 .NET；升级保留个人入口、语言、隐私、菜单模式与开机启动选择，不重启电脑。

## 范围与已知限制

- 实际 Win 键、开始按钮、其他应用下直接输入及长时间运行仍待实机验收；隔离测试不能保证已解决所有偶发显示或焦点问题。
- 曾报告文字自动删除，退出 iStart 后停止，具体原因尚未确认；本版不宣称已完成该问题的原因定位。实机输入验收前保持受影响实例退出。
- 单独 Win 取消和主动打开原生菜单仍使用带标记的 SendInput；本版仅移除焦点重试的模拟修饰键。不要在工作桌面运行 --input-test 或 --presentation-test。
- 历史来源有限，FreeCAD MRU 时间为近似依据；网络和失效目标不进入最近文件。沿用五秒界面读取预算。
- Win10/11 已知版本可在定位成功后主动启用兼容试用；只有 Win11 26200 有基本实机证据，Win10、其他 Win11、多屏及完整异常安装矩阵仍待验收。
- 当前开发电脑报告内部版本 26300，超出既有试用范围，保持 Windows 原生入口；本版未扩大版本白名单。取消启用回归使用固定系统夹具，并检查未知版本在扫描和安装钩子前拒绝接管。
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
