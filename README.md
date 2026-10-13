# iStart

面向 Windows 10/11 的个人开始菜单：集中整理软件、文件与文件夹，减少桌面混乱。

## 当前状态

当前为 **0.2.0-preview.3 试用版**。已有紧凑双栏、图标/列表切换、搜索、拖入与排序、分类管理、移除撤销、最近打开、本机软件推荐、托盘、账户电源入口和中文安装向导。

界面已按 [Figma 优化稿](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=1-1245) 应用 Material 风格：浅色背景、蓝色重点、圆角搜索/分类/按钮和内容卡片；继续使用 Windows 本机真实软件图标与中文字体，无新增 UI 依赖。查看 [实际程序预览](docs/prototypes/material-app.png) 与 [本版验证记录](docs/MATERIAL_VALIDATION.md)。

用户后续反馈开始入口出现原生或双菜单，操作时间与启用状态不确定；此项继续待复验，不能把旧专项通过记录视为当前体验问题已解决。本版仅调整界面，不修改入口钩子。

开始按钮与单独 Win 键接入、Win+R 保留、临时原生菜单、禁用恢复及测试进程异常退出恢复，已在 **Windows 11 25H2 x64 26200.9457** 通过基本实机检查。接入仅对白名单内部版本 26200 开放，默认关闭。Windows 10、其他 Win11 版本仍保持原生入口；这不是已完成双系统兼容的正式版。详见 [验证记录](docs/P0_VALIDATION.md)。

## 安装与使用

运行 `iStart-Setup-0.2.0-preview.3.exe`，按中文向导选择位置与选项；显示实际安装进度。安装到当前账户，无需管理员权限，无需另装 .NET。默认不创建桌面图标、不启用开机启动、不接管开始入口；这些选项可按需选择。安装包当前未签名。

- 把文件、文件夹或软件快捷方式拖入收藏，添加的是引用，原文件保持原位。
- 拖动条目调整顺序，拖到分类上归类；搜索结果禁止重排。分类可拖动排序。
- 右键条目可改名、重新定位、归类或移除；移除收藏可在 8 秒内撤销。右键分类可改名、排序或删除，删除分类将收藏移至未分类。
- 最近打开仅记录通过 iStart 成功交给 Windows 的启动，最多 100 项；可关闭和清除，不代表所有系统活动。
- 首次异步发现有效 `.exe` 快捷方式，最多推荐 12 项。没有可靠系统历史时显示已安装候选，之后优先参考 iStart 自身启动次数。手动收藏不被自动重排。
- 设置中可启用自定义菜单试用、开机启动及记录偏好；菜单底部可临时打开原生菜单，托盘可切回默认原生模式。
- 账户与电源操作按系统能力和策略显示；注销、关机等需确认。登录认证、Windows Hello 与安全桌面仍由 Windows 管理。

关闭窗口或点击退出会结束程序并释放接入；开机启动时显示托盘、隐藏主窗口。异常退出后的下次运行恢复原生模式。卸载保留 `%LOCALAPPDATA%\iStart\library.json` 和 `.bak`，不删除原始文件。配置 schema 1 升级到 2 后，旧验证版不支持读取新格式，请勿混用。

## 构建与验证

Windows + .NET 10 SDK；安装器使用 Inno Setup 7.1.0：

```powershell
dotnet run --project src/IStart.App/IStart.App.csproj
./scripts/Test-Prototype.ps1
./scripts/Publish-Preview.ps1
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish -TestInstall
./scripts/Test-Installer.ps1 -Package 'C:\path\iStart-QA-Setup-0.2.0-preview.3.exe' -Output "$PWD\artifacts\install-test"
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
