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
Windows 个人开始菜单预览版。本版增加联网检测新版、红点提醒及自动/手动升级。

## 更新内容与新功能

- 下载安装包：$name。安装于当前账户，包含运行时，无需另装 .NET。
- 联网后自动检查 GitHub 新版，在菜单“设置”和设置内升级入口显示小红点；升级区移到设置顶部。
- 升级页显示当前版本、目标版本、更新内容与新功能，以及实际下载进度；下载后和安装前均校验完整性。
- 默认勾选自动升级，后台下载后等待菜单收起、无设置/截图窗口且空闲一分钟再安装；取消勾选改为手动升级并立即保存。安装显示进度，不重启电脑、不强制关闭其他软件。
- 增加实际自包含 EXE 回归；核心、中英文布局、截屏、更新校验、恢复模型和隔离安装/覆盖/卸载通过本次 main 构建验证。
- 保留 Windows 11 Fluent / Google 双风格；五个导航、设置、托盘、截屏与升级页提供中英文，默认跟随系统，可手动选择简体中文或 English。软件名、文件名、网址及发布说明保留原文，安装向导仍为中文。
- 我的软件最多 30 项；最近文件合并有限本机来源；文件夹和网站可切换常用/最近排序，网站合并 Chrome/Edge 历史。我的电脑展示分区、回收站、网络及 7 个控制面板大类。
- 保留开始按钮与 Win 键接入、临时置前和原生恢复；Alt+Shift+A 后台截屏支持拖选调整、彩色标注、撤销/重做、复制与 PNG 保存，不含长截图、识别和翻译。
- 保留安装目录、开机启动选择、个人入口和设置；自动安装成功后在后台启动新版，继续原有菜单模式。
- 已知 Win10/11 版本可主动启用兼容试用，定位成功后才接入；设置可导出本机诊断。只有 Win11 26200 做过基本实机验证，Win10、其他 Win11 和多屏等验收仍未完成。
- 安装包未签名。本版为预览版；卸载保留个人收藏，托盘可恢复 Windows 原生菜单。
- 跨软件历史仅显示已有来源记录，不能保证所有应用完整历史或精确时间。
- 自动/手动策略、断网/取消/校验失败、红点及双语言布局通过隔离测试；未来公开版本间完整自动升级、生产安装后重启和异常回退仍待实测。

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
