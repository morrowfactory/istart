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
Windows 个人开始菜单预览版。本版补全「我的软件」的识别来源，修复 Chrome、Edge、LibreOffice 等软件漏项和微信图标显示问题。

## 更新内容与修复

- 结合本机桌面快捷方式、Windows App Paths 和文件及协议处理程序声明的应用标识，补全缺少开始菜单快捷方式的软件。
- 将同一套件的使用记录关联到经验证的主启动程序。LibreOffice 可按活动出现在默认列表，Writer、Calc 等组件可搜索。
- 优先使用有效的微信快捷方式显示名称与图标。图标读取失败后，后续绑定可重新读取。
- 同一应用标识的入口合并，优先使用快捷方式。共用启动程序的 GitHub、WhatsApp 等网页应用可分别显示。
- 保留 preview.29 的轻量实例通知、任务栏恢复、异常实例回退，以及 Windows 26300 主动能力检测试用；键盘手势、焦点和开始接入策略未改动。
- 本次 main 构建验证源码与自包含 EXE、后台收起/退出、原生窗口恢复、100 次左下角定位、两种风格共 20 次缓存弹出、核心数据、中英文界面、截图、升级和隔离首次/覆盖安装及卸载。
- 保留 preview.28 的先显示缓存、后台刷新与失败保留；缓存仅在本机内存，没有新增活动上传。继续保留 preview.25 的 FreeCAD/STP 历史与来源容错。
- 保留 Windows/Google 两种紧凑风格、跟随系统/中英文切换、Chrome/Edge 合并网站历史、我的电脑、彩色标注截屏、原生菜单恢复和自动/手动升级。
- 安装包 $name 用于 Windows x64 当前账户，包含离线运行环境，无需另装 .NET；升级保留个人入口、语言、隐私、菜单模式与开机启动选择，不重启电脑。

## 范围与已知限制

- 隔离通道和缓存显示的耗时不是实际任务栏/Win 键全链路延迟或冷启动保证；实际开始接管、前台输入及长时间运行仍需按系统版本验收。
- 软件列表按本机活动排序，最多显示 30 项。搜索可查找有有效入口的软件。此前报告的输入异常仍待定位。
- 本版未修改接入版本名单、键盘手势或前台锁定策略。单独 Win 取消和主动打开原生菜单仍使用带标记的 SendInput；不要在工作桌面运行 --input-test 或 --presentation-test。
- 历史来源有限，FreeCAD MRU 时间为近似依据；网络和失效目标不进入最近文件。沿用五秒界面读取预算。
- Win10/11 已知版本可在定位成功后主动启用兼容试用；只有 Win11 26200 有基本实机证据，Win10、其他 Win11、多屏及完整异常安装矩阵仍待验收。
- 当前 26300 电脑只读发现 1 个 UIAutomation 开始按钮；这不代表真实 Win/开始输入验收通过。物理多屏、混合 DPI 和任务栏自动隐藏等组合仍需实机确认。
- 安装包未签名，本版仍为预览版；卸载保留个人配置，设置可关闭接管，退出 iStart 会释放接管。

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
