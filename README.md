# iStart

面向 Windows 10/11 的个人开始菜单：集中整理软件、文件与文件夹，减少桌面混乱。

## 当前状态

当前为 **0.2.0-preview.10 试用版**。已有紧凑双栏、图标/列表切换、搜索、拖入与排序、分类管理、移除撤销、最近打开、本机软件推荐、托盘、账户电源入口和中文安装向导。

界面已按 [Figma 优化稿](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=1-1245) 应用 Material 风格：浅色背景、蓝色重点、圆角搜索/分类/按钮和内容卡片；继续使用 Windows 本机真实软件图标与中文字体，无新增 UI 依赖。查看 [Google 风格预览](docs/prototypes/theme-google.png)、[Windows 风格预览](docs/prototypes/theme-windows.png) 与 [双风格验证记录](docs/THEME_VALIDATION.md)。

0.2.0-preview.4 修复任务栏检测短暂空结果导致整个接入被停用的问题：保留 Win 键接入，开始按钮检测自动重试；鼠标快照仍受 1.5 秒有效期与窗口校验限制。用户一分钟连续交替操作反馈正常，长期稳定性仍待观察，见 [接入恢复记录](docs/RESIDENT_VALIDATION.md)。

开始按钮与单独 Win 键接入、Win+R 保留、临时原生菜单、禁用恢复及测试进程异常退出恢复，已在 **Windows 11 25H2 x64 26200.9457** 通过基本实机检查。接入仅对白名单内部版本 26200 开放，默认关闭。Windows 10、其他 Win11 版本仍保持原生入口；这不是已完成双系统兼容的正式版。详见 [验证记录](docs/P0_VALIDATION.md)。

0.2.0-preview.5 新增 Windows 风格。在“设置 → 外观风格”选择 Windows 或 Google，立即生效并保存，下次启动继续使用；升级旧配置默认保留 Google 风格。主题不会改变开始菜单接入、开机启动、收藏或排序。

## 安装与使用

运行 `iStart-Setup-0.2.0-preview.6.exe`，按中文向导选择位置与选项；显示实际安装进度。安装到当前账户，无需管理员权限，无需另装 .NET。默认不创建桌面图标、不启用开机启动、不接管开始入口；这些选项可按需选择。安装包当前未签名。

- 把文件、文件夹或软件快捷方式拖入收藏，添加的是引用，原文件保持原位。
- 拖动条目调整顺序，拖到分类上归类；搜索结果禁止重排。分类可拖动排序。
- 右键条目可改名、重新定位、归类或移除；移除收藏可在 8 秒内撤销。右键分类可改名、排序或删除，删除分类将收藏移至未分类。
- 最近打开合并 iStart、Windows Recent / 自动跳转列表、WPS 和 LibreOffice 的现成文件记录，去重后最多 100 项。其他软件历史可独立关闭；清除 iStart 记录不会删除外部历史。不读取文档内容；外部时间可能只是近似排序，不能保证所有软件都留有历史。
- 首次异步发现有效 `.exe` 快捷方式，最多推荐 12 项。没有可靠系统历史时显示已安装候选，之后优先参考 iStart 自身启动次数。手动收藏不被自动重排。
- 设置中可启用自定义菜单试用、开机启动及记录偏好；菜单底部可临时打开原生菜单，托盘可切回默认原生模式。
- 账户与电源操作按系统能力和策略显示；注销、关机等需确认。登录认证、Windows Hello 与安全桌面仍由 Windows 管理。

窗口右上角“×”仅收起到托盘；菜单或托盘的“退出”才结束程序并释放接入；开机启动时显示托盘、隐藏主窗口。异常退出后的下次运行恢复原生模式。卸载保留 `%LOCALAPPDATA%\iStart\library.json` 和 `.bak`，不删除原始文件。配置 schema 1 升级到 2 后，旧验证版不支持读取新格式，请勿混用。

## 构建与验证

Windows + .NET 10 SDK；安装器使用 Inno Setup 7.1.0：

```powershell
dotnet run --project src/IStart.App/IStart.App.csproj
./scripts/Test-Prototype.ps1
./scripts/Publish-Preview.ps1
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish -TestInstall
./scripts/Test-Installer.ps1 -Package 'C:\path\iStart-QA-Setup-0.2.0-preview.6.exe' -Output "$PWD\artifacts\install-test"
```

安装包及 SHA256 输出至 `artifacts/setup/`。`--ui-test <测试目录>` 使用独立配置打开交互验收窗口，不修改真实开机启动；一次只运行一个验收实例。`--input-test <输出目录>` 会短暂打开原生开始菜单/运行窗口，并强制结束它自己启动的测试子进程，须在空闲交互桌面手动执行，不纳入 CI。CI 检查构建、核心行为、布局及独立 QA 身份的安装/覆盖/卸载；产物保留 7 天，不自动发布 Release。

Inno Setup 7 构建工具有商业使用许可要求，商业发行前需核对并取得适用授权；未复制 Open-Shell 代码。

运行时支持与应用实测需分别判断：当前 .NET 10 官方 Windows 10 支持表以仍受支持的企业/LTSC 版本为主，不能据此承诺普通 Win10 Home/Pro 的长期支持；Win10 具体版本与运行时策略列为兼容验证项。[.NET 官方系统矩阵](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) [Inno 许可](https://jrsoftware.org/isorder.php)

## 文档与下一步

- [架构](ARCHITECTURE.md)：模块、数据、接入及恢复边界。
- [愿景](VISION.md)：用户体验、隐私与兼容原则。
- [执行计划](EXECUTION_PLAN.md)：阶段进展与未通过门槛。
- [验证记录](docs/P0_VALIDATION.md)：真实结果、限制与回退。
- [当前系统实机验收](docs/CURRENT_SYSTEM_VALIDATION.md)：真实鼠标操作、恢复、能力与未执行项。

下一步优先补齐 Win10/其他 Win11、Explorer 重启、多屏缩放、自动隐藏、交互式安装取消及失败回退。当前不能将局部实机结果视为正式发布验收通过。

本机 runtime.log 仅记录启停、模式和检测恢复事件，不记录按键、文件内容或收藏路径；超过 64 KiB 轮转并仅保留一份旧日志。

0.2.0-preview.6 统一开始按钮、Win、托盘与重复启动的窗口显示路径：恢复最小化窗口、自定义菜单可见时使用浮层、收起后解除置顶，并记录前台激活结果。后台启动不申请焦点，也不会在异步初始化结束后再次隐藏用户刚打开的菜单。详见 [窗口显示验证](docs/FOREGROUND_VALIDATION.md)。

preview.6 已在当前电脑确认：菜单贴近开始按钮、图标不带快捷箭头，打开后可直接输入英文与中文。详见 [窗口与焦点验收](docs/FOREGROUND_VALIDATION.md)。

0.2.0-preview.7 搜索会即时匹配全部已发现软件及全部收藏，不受推荐前 12 项或当前分类限制；软件结果可滚动，无结果会提示。商店应用通过 Windows AppsFolder 发现，同时匹配显示名和应用标识（例如显示为 ChatGPT 的 Codex）。关闭推荐只关闭推荐展示，仍在本机整理软件搜索入口；不扫描全盘文件、不上传应用列表。商店应用支持打开，暂不支持拖入固定。

preview.8 补齐商店应用的真实图标读取，读取失败显示通用应用图标；当前电脑的 Codex 图标已成功读取和检查。

preview.9 补充 Windows 最近文档，在选择最近打开或重新打开该视图时刷新。系统没有提供记录时明确提示；不读取各软件内部历史或 Jump List，不扫描全盘。

preview.10 将外部最近文件扩展至 Windows 自动 Jump List、LibreOffice PickList 和 WPS RecentFiles。本机实测合并 100 条有效记录，含 13 张图片和 45 个办公文档；不会扫描全盘或监控其他软件的打开动作。
