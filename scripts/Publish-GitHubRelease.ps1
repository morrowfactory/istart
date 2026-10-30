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
Windows 个人开始菜单预览版。本版统一从屏幕工作区左下角打开 iStart，修复首次启动和原生模式下经常居中的问题。

## 更新内容与修复

- 首次启动、应用图标、托盘、重复运行和已接管的开始入口统一从所选屏幕工作区左下角弹出，保留最多 8 个物理像素间距。
- 移除“接管已启用才定位”的条件；原生模式和暂不支持接管的系统版本也使用相同定位，不再沿用旧居中或手动拖动位置。
- 主窗口改为 Manual 初始位置，首次可见启动进入统一显示流程；显示前定位，并在显示后按更新的 DPI 校正，菜单不跟随居中的任务栏开始按钮。
- 激活窗口前先确定显示器；托盘按鼠标所在屏幕打开，其他入口使用显示前的前台窗口屏幕。保留工作区边界和最小尺寸保护。
- 后台启动共用定位方法，仍不请求激活并立即隐藏。
- 独立非活动桌面已完成 Windows/Google 原生模式共 100 次显示/隐藏，覆盖首次可见边界、旧居中位置、明确屏幕坐标和 10 次最小化恢复；后台隐藏检查通过。
- 本次 main 构建验证核心数据、自包含 EXE、中英文双主题、截图、升级夹具及隔离首次安装、覆盖安装和卸载。
- 保留 preview.26 的后台图标、扫描合并和焦点重试改进，以及 preview.25 的 FreeCAD/STP 历史、来源容错及文件夹搜索。
- 保留 Windows/Google 两种紧凑风格、跟随系统/中英文切换、Chrome/Edge 合并网站历史、我的电脑、彩色标注截屏、原生菜单恢复和自动/手动升级。
- 安装包 $name 用于 Windows x64 当前账户，包含离线运行环境，无需另装 .NET；升级保留个人入口、语言、隐私、菜单模式与开机启动选择，不重启电脑。

## 范围与已知限制

- 实际 Win 键、开始按钮、其他应用下直接输入及长时间运行仍待实机验收；隔离测试不能保证已解决所有偶发显示或焦点问题。
- 曾报告文字自动删除，退出 iStart 后停止，具体原因尚未确认；本版不宣称已完成该问题的原因定位。实机输入验收前保持受影响实例退出。
- 单独 Win 取消和主动打开原生菜单仍使用带标记的 SendInput；本版仅移除焦点重试的模拟修饰键。不要在工作桌面运行 --input-test 或 --presentation-test。
- 历史来源有限，FreeCAD MRU 时间为近似依据；网络和失效目标不进入最近文件。沿用五秒界面读取预算。
- Win10/11 已知版本可在定位成功后主动启用兼容试用；只有 Win11 26200 有基本实机证据，Win10、其他 Win11、多屏及完整异常安装矩阵仍待验收。
- 当前开发电脑报告内部版本 26300，超出既有接管试用范围，保持 Windows 原生开始入口；本版修正 iStart 窗口定位，没有扩大系统接管白名单。物理多屏、混合 DPI 和任务栏自动隐藏等组合仍需实机确认。
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
