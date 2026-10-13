# iStart 架构说明

## 状态与约束

当前使用 .NET 10、C# + WPF，单一桌面项目，无额外 NuGet 依赖；Windows Forms 仅提供托盘和显示器/睡眠能力。Inno Setup 7.1.0 构建当前用户的中文自包含安装包。目标是 Win10/11 x64，开始入口现仅允许内部版本 26200 试用，其余版本保持原生模式。实际证据见 [验证记录](docs/P0_VALIDATION.md)。

## 模块边界

| 模块 | 已实现职责与边界 |
| --- | --- |
| MainWindow / Editing / Shell / Websites | 四个固定入口、搜索、图标/列表、添加与隐藏、同页活动排序、设置、托盘与定位；普通用户运行 |
| LibraryStore | 收藏、分类、偏好、最近记录；复制后修改，临时写入与原子替换，保留一份上一版本；失败不替换内存 |
| SoftwareDiscovery / ShellShortcut | 异步发现用户与公共开始菜单 `.lnk`；IShellLinkW/IPersistFile Unicode 解析；有效 exe 目标去重，过滤安装维护与失效入口 |
| ShellIconConverter | Shell 图标提取与有界缓存；UNC 使用通用图标，避免远程图标读取 |
| StartMenuAdapter / WinInput | UIAutomation 定位 StartButton；低级键盘/鼠标钩子处理单独 Win 和开始按钮；组合键保留，原生注入使用自有标记避免递归 |
| NativeMenu | SendInput 发送带标记的 Ctrl+Esc 请求临时原生菜单 |
| SystemActions | 系统策略/能力判断，锁定、注销、切换用户、睡眠、休眠、关机和重启；固定参数，不使用 /f |
| App | 当前 SID 命名互斥锁与显示/退出事件；已有实例正常退出等待；后台启动与诊断模式；ui-test 使用独立配置并禁用真实开机启动修改 |
| installer / scripts / CI | 中文向导与真实文件安装进度、升级前正常退出、卸载与校验值；QA 安装使用独立 AppId，无生产部署 |

进程不注入 Explorer、不修改系统二进制、不替换登录认证、不添加服务或驱动。临时原生打开与默认原生切换是独立操作。

Appearance 统一提供 Windows/Google 两套颜色与形状资源，App.xaml 与 MainWindow.xaml 通过 DynamicResource 即时更新按钮、输入框、列表选择与内容卡片。设置中的外观选择单独保存并调用 SetTheme，不重启适配器，也不改写隐私或开机启动选项。继续使用 WPF 与 Microsoft YaHei UI、本机 Shell 图标，无额外 UI 库。输入框保留 PART_ContentHost、列表项保留拖入边框和键盘焦点提示；配置格式、系统接入与恢复路径不因换肤改变。设计来源和实测见 [界面验证记录](docs/MATERIAL_VALIDATION.md)。

## 本地数据

`%LOCALAPPDATA%\iStart\library.json` 当前 schemaVersion=2，支持读取 schema 1 并迁移；未知版本拒绝加载。JSON 结构无效时读 `.bak`，不把未知新版本悄悄换成旧备份。

- Entries：id、target、title、categoryId，数组顺序表示手动排序。
- Categories：id、name，数组顺序表示分类排序，未分类不可删除。
- Launches：target、title、openedAt、count，最多 100 个唯一目标，只表示成功交给系统启动。
- 偏好：推荐开关与排除项、记录开关、自定义入口、开机启动、图标视图、Theme（Windows/Google；缺失或未知值回退 Google）。

配置仅保存在本机；移除入口不删除源文件，删除分类将收藏移至未分类。手动排序与推荐排序独立。推荐候选不持久化，最多显示 12 项；可靠系统历史尚无来源，不读取 UserAssist 或宣称完整使用频率。商店应用补充 AppsFolder 发现，无有效 Shell 入口的软件不保证发现。

## 接入与恢复

StartMenuAdapter 在后台单次扫描任务栏，缓存 StartButton 边界；钩子回调只检查事件与边界并派发 UI 工作，不读配置或运行耗时扫描。鼠标检查窗口根句柄、实时任务栏矩形与缓存新鲜度。运行期间空结果或检测异常只更新重试状态，不撤销用户偏好或停用键盘钩子；保留的旧快照仍须通过 1.5 秒新鲜度与窗口校验，过期则鼠标透传，成功扫描后自动恢复按钮接入；超时未完成的扫描不会反复新建并行扫描任务。

单独 Win 松开时抑制原生弹出并显示菜单，Win 组合键透传；临时原生请求标记注入避免递归。启用前写 session.active，正常退出删除；下次运行发现遗留标记时释放接入、保存原生偏好并提示。进程退出由 Windows 清理所属钩子，已通过独立测试子进程异常退出的原生恢复检查。

低级钩子可能被 Windows 因回调超时移除，当前尚无可靠失效检测；Explorer 重启、多屏/自动隐藏/缩放、锁屏和睡眠恢复仍待隔离验收。版本白名单不是完整兼容性证明。[微软低级键盘钩子说明](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)

## 账户与电源

账户策略读取当前用户/计算机 Policies，睡眠/休眠查询系统能力，不可用操作不显示。关机与重启固定使用 `/t 0` 且无 `/f`，注销等执行前产品界面确认。当前只测试参数与能力查询，未在日常电脑实际执行破坏会话的操作；不能声称完整账户电源验收通过。

## 安装生命周期

中文欢迎、位置、选项、实际文件进度、完成页；当前账户安装，无管理员提权。默认不创建桌面图标、不开机启动、不接管入口，可选启动和接入。安装成功与菜单接入成功分开；兼容检查在实际用户程序执行，失败保留原生入口。

升级前和卸载前调用自身 `--exit`，等待当前用户实例释放互斥锁，超时中止并提示；不强杀用户程序。卸载删除本次安装程序及开机启动值，保留收藏。QA 构建隔离 AppId，跳过真实进程退出/启动设置，不会关闭正在使用的正式 iStart。

安装器复用 Inno 的取消、错误和回滚机制，但尚未完成磁盘不足、权限拒绝、运行文件占用与中断回退的实机验证；不承诺任意升级故障都能自动恢复完整旧版本。签名和自动更新未实现，正式分发前核对 Inno 商业许可及 .NET 随附许可。

## 验证与发布门槛

SmokeTest 验证数据保护、迁移、顺序、发现过滤、最近记录和 WPF 列表/图标渲染。InputIntegrationTest 是交互桌面专项测试，包含原生基线、Win/按钮/组合键/临时原生/禁用/异常进程恢复。Test-Installer 只接受隔离 QA 安装包与 artifacts 内新目录，验证静默首次、覆盖、卸载。

Windows CI 保存核心结果、布局图、安装校验值及安装结果 7 天，不自动发布正式版本。Win10/其他 Win11、多屏缩放、Explorer 重启及安装失败矩阵未通过前，保持试用与版本限制。完整交付门槛见 [执行计划](EXECUTION_PLAN.md)。

当前系统的真实鼠标、重新定位与完整菜单进程恢复证据见 [实机验收](docs/CURRENT_SYSTEM_VALIDATION.md)；按键人工反馈和会话中断项目仍待完成。

窗口 Closing 默认取消关闭并 Hide 到托盘；显式 RequestExit（菜单、托盘、升级/卸载信号）和系统会话结束允许退出。RuntimeLog 记录生命周期与适配器状态，每份约 64 KiB，仅保留一份旧日志，不记录按键或文件路径。接入修复证据见 [恢复记录](docs/RESIDENT_VALIDATION.md)。

ShowMenu 为用户触发的统一显示入口：恢复 Normal、开启 ShowActivated、临时提升层级并请求 SetForegroundWindow，再聚焦搜索。自定义菜单保持浮层至隐藏；普通独立窗口不保留置顶。呈现期间避免同步 Deactivated 误隐藏；隐藏时清除 Topmost。后台启动的隐藏在首次 Show 后同步完成，不放在异步接入初始化的末尾。前台权限可能被 Windows 拒绝，日志只记录成功标志，不修改系统焦点锁定策略、不借用其他线程输入队列。

搜索呈现完成后在本线程设置原生 HWND 焦点，再同步 WPF 键盘与逻辑焦点。前台请求失败且修饰键未按住时才使用带标记的平衡 Alt/Ctrl 序列重试。菜单位置按任务栏与物理工作区计算；快捷方式图标读取资源或目标，启动路径不变。

软件发现补充只读 Shell.Application / AppsFolder 枚举，保留带 ! 的商店应用标识；COM 对象逐项释放，沿用取消令牌与五秒预算。传统快捷方式和商店应用共同组成搜索候选，输入时按名称及入口标识过滤，推荐仍限 12 项。商店应用通过 explorer.exe 的单个 ArgumentList 参数启动；不当作磁盘文件固定。关闭推荐不关闭本机搜索索引。收藏搜索跨分类，文件范围仅限收藏。

商店应用图标：SHParseDisplayName 将 AppsFolder 入口解析为 PIDL，SHGetFileInfo 使用 SHGFI_PIDL 与 SHGFI_ICON 获取图标；转为冻结位图并沿用 256 项缓存，finally 释放 HICON 和 PIDL。失败回退通用 exe 图标，不把虚拟入口当作磁盘路径。

RecentDocuments 在最近视图中异步读取当前用户 Recent 文件夹的最多 200 个最新 .lnk，五秒等待预算；读取目标和快捷方式修改时间，过滤网络路径、失效目标、文件夹及 exe/lnk。按目标去重并与 iStart Launches 合并至最多 100 项。外部历史只在内存展示，不导入 library.json，不更改系统隐私设置；preview.10 扩展读取下面列明的现成历史来源。ShowWindowsRecentDocuments 独立控制展示和读取，旧配置默认启用；关闭时清空内存结果。Windows 快捷方式时间仅是排序依据，不宣称精确最后打开时间。

跨软件历史（preview.10）：JumpListDocuments 只读打开最新 60 个 AutomaticDestinations 结构化存储文件（单文件 ≤16 MiB、最多 200 个流、单流 ≤1 MiB），通过 IPersistStream 读取内嵌 Shell Link 目标，禁止 Resolve 和执行。COM IStorage / 枚举器 / IStream 均 finally 释放。仅用流或容器修改时间作近似排序；不解析 DestList 版本、不声称精确打开时间。暂不读 CustomDestinations。

LibreOfficeDocuments 只读当前用户 4/user/registrymodifications.xcu（≤8 MiB），禁用 XML DTD 与外部解析，只取 PickList/OrderList/HistoryItemRef 文件 URI；保留序号顺序，用配置修改时间作近似排序。WpsDocuments 只读 HKCU/Software/Kingsoft/Office/6.0/{wps,et,wpp,pdf,ofd}/RecentFiles/files 的最多 100 个数值子键 path；不读取关联账户、设备或云记录。WPS 未确认打开时间，因此使用未知时间并保留软件序号，不伪造时间戳。

各来源分别容错；网络、已删除、文件夹和可执行/脚本目标不加入外部文档结果。合并最多 100 项，关闭外部历史后不保留迟到读取结果。读取配置和链接元数据，不读取用户文档内容、不写入外部软件数据或系统历史。

软件推荐展示（preview.11）：RecommendationsEnabled 新配置默认 false；SoftwarePanel 整体 Collapsed，不保留布局空间。移除菜单内推荐开关与对应处理器，设置是唯一启用入口。标题和说明只在主动搜索时显示，推荐模式仅展示图标。查询非空时搜索结果独立于推荐偏好，清空后按偏好收起。当前用户设置同步关闭，其他收藏、分类、主题和接入保留。

收藏分隔（preview.12）：无搜索的全部收藏同时含两类入口时，ListCollectionView 使用 EntryGroups 视觉分组，程序在上，文件/文件夹在下。Shell 应用、可执行文件/脚本及指向程序的快捷方式归程序；其余归文件类。分类结果有界缓存，源 Entries 不排序改写。GroupStyle 外层纵排、组内沿用图标 WrapPanel 或列表面板，只有第二组渲染横线；单类视图、分类、最近和搜索不增加分隔。

Windows 11 Fluent（preview.13）：Appearance 继续统一管理双主题，增加主题专属内边距、搜索边框、按钮文字、菜单字体、图标尺寸及状态区资源。Windows 入口宽度缩至 64 DIP（含列表容器边距后默认宽度六列），小窗口自动换行；Google 沿用原资源值。CategoryIconConverter 仅加载四个冻结导航位图，搜索/账户图标同样使用本地嵌入资源。Assets/Fluent 保留 Figma 导出的原 SVG 和派生 PNG；程序嵌入 PNG，无运行时网络请求、SVG 渲染库或新增应用依赖。搜索 PART_ContentHost 与焦点/接入逻辑不变。

窗口继续使用系统标题栏、边框和阴影；本版应用浅色层次，未启用原生 Mica 模糊。Figma 字体环境只有 Noto Sans SC，设计稿使用该替代字体；Windows 程序使用 Segoe UI Variable 并回退 Microsoft YaHei UI。Google 字体沿用原值。运行时文字和文件夹内容保持真实数据，不替换成稿中的示例名称。

GitHub 发布边界：build.yml 将 ProductVersion 绑定检出 SHA；release.yml 手动接收最新 main SHA、成功 push/main 构建编号与授权记录，仅下载安装包允许清单。Publish-GitHubRelease.ps1 校验版本、SHA256 和源工作流，先草稿后公开，并下载公开文件复验；release-manifest.json 保存精确来源和保护状态。发布串行、GitHub Token 最小权限，无远程服务器或新增生产密钥。当前缺少强制分支/环境审批，本次使用会话授权，详见 docs/GITHUB_RELEASE.md。

统一品牌资源（preview.14）：Assets/Brand 保存 Figma 矢量源与透明 PNG、九档 ICO。MainWindow 在双主题中共用品牌 PNG，窗口 Icon 与托盘使用嵌入 ICO，ApplicationIcon 将同一图标编译进 EXE；安装器使用该 ICO，并将独立 istart.ico 安装给桌面/开始菜单快捷方式和卸载列表。托盘退出时释放克隆图标。SVG 仅在维护时转换，无新增运行时依赖，详见 docs/BRAND_ICON.md。

最近文件与文件夹（preview.15）：RecentDocuments.Files 在合并前过滤自身及外部记录，仅让有效文档进入最近视图。FrequentFolders 复用 ShellShortcut 与 JumpListDocuments 的文件夹读取模式，独立读取有效本机目录；主窗口在现有 5 秒异步任务中刷新文件和目录缓存。常用文件夹视图保留手动项，再以打开次数/近期文档目录活动和近似时间补充空位；建议与本次隐藏仅驻留内存，固定时才保存入口。沿用外部历史开关，关闭时两类缓存一起清空；无配置 schema 变更。见 docs/RECENT_FILES_FOLDERS.md。

活动视图（preview.16，取代上面的历史分类/推荐界面）：MySoftware 精确匹配本机累计 AppSwitched 与 iStart 记录，最多 30 个软件；SoftwareDiscovery 补充 exe 和 AppUserModel.ID 元数据，不再渲染推荐区域。四个固定导航不显示旧分类，原 Entries/Categories 仍保留以兼容。FrequentFolders 默认活动降序，可切换直接目录记录的最近排序；不会修改保存数组。WebsiteHistory 以系统 winsqlite3 只读查询 Chrome/Edge 的本机短暂快照，按域名聚合；MainWindow.Websites 处理异步刷新及同页常用/最近切换。两种排序偏好保存；浏览器历史和建议驻留内存，快照 finally 清理，无 NuGet 或运行时网络依赖。设置可关闭网站读取，异常/部分失败明确提示。数据字段与读取边界见 docs/ACTIVITY_VIEWS.md。
