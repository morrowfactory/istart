[English](../../README.md) | [简体中文](README.md)

# iStart

把常用软件、最近文件、文件夹和网站集中到一个紧凑的 Windows 个人开始菜单，让桌面更清爽。

**版本：0.2.0-preview.25 预览版**

本版修复有使用记录的软件及最近文件遗漏，包括便携版 FreeCAD 与 STP/STEP 模型；改进历史来源容错、文件夹搜索和活动刷新，保留紧凑 Windows/Google 界面及自动/手动升级。安装包包含离线运行环境和中英文资源；体积优化边界见[安装包优化](PACKAGE_SIZE.md)。

界面默认跟随 Windows 显示语言，可手动选择简体中文或 English，立即应用并保存。中文系统使用简体中文，英文及暂未支持的系统语言使用英文。主菜单、设置、托盘、截屏和升级页提供中英文；软件名、文件名、网址及发布说明保持原文。详见[语言说明与验证](LOCALIZATION.md)。

[下载 Windows x64 安装包](https://github.com/morrowfactory/istart/releases/download/v0.2.0-preview.25/iStart-Setup-0.2.0-preview.25.exe) · [版本与校验文件](https://github.com/morrowfactory/istart/releases/tag/v0.2.0-preview.25) · [所有版本](https://github.com/morrowfactory/istart/releases)

**preview.25 修复：**补充无开始菜单快捷方式的已使用软件，读取 FreeCAD 保存的最近文档，避免失效旧路径占据有效发现名额，隔离历史来源错误，文件夹先搜索后限额，并在返回“我的软件”时刷新活动。详见[活动视图](ACTIVITY_VIEWS.md)。既有[双风格](THEME_VALIDATION.md)、[升级](UPDATES.md)和[接入恢复](RESIDENT_VALIDATION.md)继续保留。

## 当前功能

preview.21 加固 Win10/11 开始入口：输入钩子使用专用消息线程，减少主界面卡顿导致的失效；Win10 原生 Start 按钮点击时重新验证位置和 HWND。任务栏重建、显示与系统设置变化会丢弃旧定位，自动化检测超时可使用原生窗口后备；启动时短暂重试。界面超过约 2 秒不响应时放行后续原生输入。目标仍是已知 Win10/11 版本，尚不代表 Win10 实机通过。

同版修复从 iStart 图标打开时被其他窗口遮挡：原生模式也使用临时浮层和有界焦点重试，切到其他窗口后收起。重复运行快捷方式会向已有实例转交前台权限。底部“启用接入”可主动接管系统开始按钮；未启用时系统开始按钮仍打开原生菜单。

preview.20 按企业微信截图习惯调整：默认全局快捷键 **Alt+Shift+A**，后台运行时也可触发；设置中可改用 Ctrl+Alt+A、Ctrl+Shift+S 或关闭。注册失败时明确显示冲突，不抢占其他软件快捷键。工具栏改为紧凑图标，双击选区或 Enter 完成复制，右键先重选、再次右键取消；Ctrl+Z 撤销、Ctrl+Y 重做、Ctrl+C 复制、Ctrl+S 保存、Esc 取消。全局快捷键截取鼠标所在显示器；菜单按钮仍截取菜单所在显示器。

preview.19 新增“截屏”：菜单顶部点击后隐藏 iStart，拖选当前显示器的范围；支持移动选区、拖动边角缩放，以及彩色箭头、圆圈、方框、画笔、文字和矩形马赛克。工具栏跟随选区，点击“完成”复制图片或保存 PNG；Esc 取消、Ctrl+Z 撤销、Ctrl+C 复制。文字按 Enter 完成，Shift+Enter 换行。截图不会自动保存或上传；不包含文字识别、翻译和滚动长截图。

菜单包含“我的电脑”和应用内升级。公开安装包与校验信息见上方发布链接。

| 入口 | 显示内容 | 排序与限制 |
| --- | --- | --- |
| 我的软件 | 有本机活动依据或手动添加的软件 | 使用多的在前，最多 30 个；搜索也最多 30 个 |
| 最近打开 | iStart、Windows、WPS、LibreOffice、FreeCAD 等留下记录的有效文件 | 最近优先、去重，最多 100 项；不显示文件夹和程序 |
| 常用文件夹 | 已添加目录及本机活动中的目录 | 默认常用优先，同页按钮切换最近打开；按窗口空位补充 |
| 我的网站 | Chrome 和 Edge 的有限本机浏览历史，按域名合并 | 默认常用优先，同页按钮切换最近网址清单，最多 30 个 |
| 我的电脑 | 硬盘分区、可用容量、回收站、网络、可展开的 7 个控制面板大类（不含轻松使用） | 点击系统项目打开 Windows 原生页面；支持当前页搜索 |

活动记录是有限的本机依据，不是全系统完整统计；FreeCAD 来源使用保存的 MRU 列表，不能凭空恢复缺失或尚未保存的记录。详见[读取范围](ACTIVITY_VIEWS.md)。

- Windows 11 Fluent / Google 两种风格，在设置中即时切换并保存。
- 桌面、任务栏、窗口、托盘、安装器和菜单使用统一 iStart 图标。
- 软件和文件夹可拖入添加；支持图标/列表显示、搜索和隐藏。
- 保留 Windows 原生菜单恢复、账户、电源和托盘入口。
- 已移除独立软件推荐区、未分类、新建分类和分类名称输入框；旧入口数据保留兼容。

![五个入口与我的电脑](../prototypes/computer.png)

界面示意使用测试磁盘容量；实际内容来自当前账户的本机系统与活动记录。详见[活动视图说明](ACTIVITY_VIEWS.md)。

## 安装与使用

1. 下载并运行安装包，按中文向导选择位置与选项，查看实际安装进度。
2. 安装仅用于当前账户，无需管理员权限，也无需另装 .NET。新安装默认不创建桌面快捷方式、不启用开机启动、不接管开始入口；可按需勾选。
3. 从五个入口访问内容，直接输入搜索文字；拖入软件快捷方式和文件夹可添加入口。
4. 在“设置 → 外观风格”选择 Windows 或 Google。网站与文件夹页面的按钮可切换常用/最近，选择会保存。
5. 在“设置 → 界面语言”选择跟随系统、简体中文或 English，无需重启；旧配置升级后默认跟随系统。

从 preview.24 开始，安装版在启动联网时及后续定时检查 GitHub 新版，在“设置”和升级按钮显示红点。“版本与升级”位于设置顶部，升级页显示当前版本、目标版本、更新说明及下载进度。**默认勾选自动升级**，旧配置也采用此默认值：后台下载并校验，菜单已收起、没有设置或截图窗口、当前 Windows 会话空闲一分钟后安装。取消勾选则只提醒，手动下载与安装；选择立即保存。安装显示进度，保留个人入口和设置，不重启 Windows、不强制关闭其他软件。仅接受 `morrowfactory/istart` Releases；预览版可跟随更高预览或正式版，正式版只接收正式版。开发和测试运行不启动后台升级或执行安装器。详见[升级与验证](UPDATES.md)。

安装包目前未签名。升级保留原有入口、记录和有效设置；旧分类数据保留，但不再显示分类编辑界面。隐藏入口不会卸载软件、移动原文件或删除浏览器历史。

## 原生菜单与退出

- 底部“Windows 原生菜单”：临时打开系统菜单。
- 托盘“切回 Windows 原生菜单”：停用自定义接入，恢复默认原生模式。
- 右上角“×”：收起到托盘；菜单或托盘的“退出”才结束程序并释放接入。
- 上次异常退出时，普通启动恢复原生模式；明确的 --enable-menu 请求可重新启用，包括已有后台实例。正常注销/关机时先释放接入、清理标记并保留偏好。卸载默认保留个人配置。

账户与电源操作遵循系统能力和策略，注销、关机等需要确认；登录认证、Windows Hello 和安全桌面仍由 Windows 管理。

## 兼容性与已知限制

本版增加已知 Win10/11 版本的能力检测试用和设置内诊断导出；preview.17 及更旧版本仍只允许 26200 接入。

目标为 Windows 10/11 x64，目前仍是预览版，不能视为已完成双系统兼容验收。

| 系统 | 当前验证范围 |
| --- | --- |
| Windows 11 25H2 x64，26200.9457 | 开始按钮、单独 Win 键、Win+R 保留及基本恢复已实机检查；基本接入与恢复已实机验证；继续优先使用 UIAutomation 定位 |
| Windows 10 19041–19045；Windows 11 22000、22621、22631、26100 | 允许用户主动启用兼容试用，成功定位按钮后才安装钩子；尚无对应系统实机验收 |
| 其他版本、定位失败或异常恢复 | 保持原生入口；不猜测开始按钮坐标 |

设置中的“导出开始菜单兼容诊断”保存版本策略、显示器数量、按钮数量/边界与识别方式，不包含软件列表、文件路径或网址，不上传数据。详细测试范围见[Windows 兼容说明](WINDOWS_COMPATIBILITY.md)。

Explorer 重启、多屏缩放、自动隐藏任务栏、长期稳定性，以及交互式安装取消/失败回退仍需进一步验证。运行时支持与应用实测需分别判断，参见 [.NET 官方系统矩阵](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)。

## 本地数据与隐私

- 配置位于 `%LOCALAPPDATA%\iStart\library.json`，保留一份 `.bak`。卸载不主动删除个人入口和记录。
- 活动整理在本机进行，不上传软件列表、文件路径或浏览历史，不扫描全盘。升级器自动检查固定公开 GitHub 仓库，可在设置关闭自动下载。这些请求不发送本机活动数据。
- 软件排序参考 Windows 累计应用切换与 iStart 打开记录；目录排序参考打开次数及近期文件活动，均不是完整系统频率或精确月统计。
- 网站只读取 Chrome/Edge 保留的有限历史，不读取 Cookie、密码、网页内容或无痕会话；同域名合并后打开网站根地址。
- 设置可关闭外部文件/文件夹活动或浏览器历史读取。“清除 iStart 记录”不会清除外部历史。
- 浏览器历史快照仅在本机临时使用，正常读取后清理；不可读、部分失败或超时会提示。
- 升级下载缓存在 `%LOCALAPPDATA%\iStart\Updates`；取消或失败清理本次下载。成功后清理超过 7 天的旧更新缓存，至少保留最近两次下载，不清理个人数据。
- `runtime.log` 记录运行与恢复诊断，不记录按键、网址或文件内容；超过 64 KiB 轮转，只保留一份旧日志。

## 本地开发目录

本机项目主目录为 `E:\workspace\istart`。开发工具放在 `.istart-tools/`，虚拟机与私有测试资料放在 `.istart-testlab/`，设计预览放在 `artifacts/design-previews/`；三者均被 Git 忽略，不进入发布资产。开发命令从项目根目录执行，生成文件写入 `artifacts/`。

原来位于工作区旁的工具、测试目录与原设计预览目录保留兼容目录联接，实际文件均在项目内，不占用两份空间。已安装程序和 `%LOCALAPPDATA%\iStart` 仍使用原有位置。

本机发布文件只保留最新 **5 个已发布版本**，每版在 `artifacts/releases/<version>/` 保存一套经过校验的正式安装包、SHA256 和来源清单。确认保留副本有效后，清理过期或重复的本地/QA 安装包及旧测试运行库。源码/Git 历史、开发工具、虚拟机基线、个人恢复备份和必要验收记录不受这个版本数量限制；GitHub 公开 Release 的保留策略不变。

## 构建与验证

需要 Windows、.NET 10 SDK；构建安装包另需 Inno Setup 7.1.0。

```powershell
dotnet run --project src/IStart.App/IStart.App.csproj
./scripts/Test-Prototype.ps1
./scripts/Publish-Preview.ps1
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish -TestInstall
./scripts/Test-Installer.ps1 -Package "$PWD\artifacts\setup\iStart-QA-Setup-0.2.0-preview.25.exe" -Output "$PWD\artifacts\install-test"
```

安装包及 SHA256 输出到 `artifacts/setup/`。QA 安装器使用隔离身份；验证输出目录必须位于 `artifacts/` 且尚不存在。

`0.2.0-preview.23` 保留自包含运行环境、中英文资源及原有功能，只发布当前支持的框架语言资源，不分发 PDB 调试符号，安装器使用 `lzma2/ultra64` 固实压缩。重新打包先清理限定的发布输出目录，防止旧语言资源混入；CI 另运行自包含程序回归。体积对比与边界见[安装包优化](PACKAGE_SIZE.md)。

`--probe-system <目录>` 只读检查驱动器、控制面板项目数量和 GitHub 可升级版本，不启动系统项目或安装器。

`--ui-test <目录>` 使用独立配置验收界面，不修改真实开机启动；`--probe-activity <目录>` 只输出活动来源数量。`--input-test <目录>` 会操作原生菜单/运行窗口并结束它自己启动的测试子进程，仅在空闲交互桌面手动执行，不纳入 CI。

GitHub CI 验证构建、核心行为、布局及隔离安装/覆盖/卸载，临时产物保留 7 天。公开安装包通过[发布流程](GITHUB_RELEASE.md)使用最新 main 的已验证 CI 产物，核对来源 SHA 和哈希后发布；不包含个人配置或历史。

Win10 桌面兼容测试使用独立虚拟机，运行 `scripts/Test-WindowsVm.ps1`；宿主机可用 `-PreflightOnly` 只读预检。环境隔离、安装包校验和人工验收矩阵见 [Windows 测试环境](WINDOWS_TEST_LAB.md)。脚本与环境准备不代表 Win10 验收通过。

Inno Setup 7 有商业使用许可要求，商业发行前需取得适用授权，参见 [Inno 许可](https://jrsoftware.org/isorder.php)。项目未复制 Open-Shell 代码。

## 项目文档

GitHub 默认文档为英文，完整中文版本单独位于 `docs/zh-CN/`。每页顶部可切换 English / 简体中文，文件和目录使用英文名称或语言代码。查看[完整文档目录](INDEX.md)。

- [架构](ARCHITECTURE.md)：模块、存储、活动来源和恢复边界。
- [愿景](VISION.md)：产品方向、体验与隐私原则。
- [执行计划](EXECUTION_PLAN.md)：阶段进展和待完成验收。
- [活动视图](ACTIVITY_VIEWS.md)：软件、目录、网站的排序、字段与读取限制。
- [统一图标](BRAND_ICON.md)：Figma 来源和资源维护。
- [窗口与焦点验证](FOREGROUND_VALIDATION.md)：置前、定位与直接输入。
- [当前系统实机验收](CURRENT_SYSTEM_VALIDATION.md)：历史实测范围与未执行项。
- [我的电脑与升级验证](COMPUTER_UPDATE_VALIDATION.md)：系统项目、更新来源与已测范围。
- [界面语言](LOCALIZATION.md)：系统语言回退、切换与验证边界。
- [GitHub 发布](GITHUB_RELEASE.md)：版本来源、完整性校验与回退。
