[English](../../ARCHITECTURE.md) | [简体中文](ARCHITECTURE.md)

# iStart 架构说明

## 本地开发目录

本机源码与开发输出统一放在 `E:\workspace\istart`：`.istart-tools/` 保存 SDK/编译器，`.istart-testlab/` 保存虚拟机磁盘、镜像与私有测试资料，`artifacts/design-previews/` 保存交互设计预览。三者均被 Git 忽略，不进入公开发布资产。旧工具/虚拟机注册路径与预览链接通过目录联接兼容，不保存重复数据；已安装程序与用户配置仍使用原有位置。

本机发布保留策略独立于应用数据和远程 Release：`artifacts/releases/<version>/` 保留最新五套已发布的安装包、校验文件与来源清单，核验后清理过期或重复的生成产物。开发工具、虚拟机基线、配置备份和必要诊断证据继续保护。

## 状态与约束

当前使用 .NET 10、C# + WPF，单一桌面项目，无额外 NuGet 依赖；Windows Forms 仅提供托盘和显示器/睡眠能力。Inno Setup 7.1.0 构建当前用户的中文自包含安装包。目标是 Win10/11 x64，开发版的开始入口允许 Win10 19041–19045 及 Win11 22000/22621/22631/26100/26200/26300 能力检测试用，其他版本保持原生模式；只有 26200 做过基本实机接入验证，26300 目前仅有按钮只读发现和夹具证据。实际证据见 [验证记录](P0_VALIDATION.md)。

## 模块边界

常驻菜单复用同一界面树、列表、搜索、选中项和滚动位置。ShowMenu 只处理定位、显示和焦点，再安排 200 毫秒 ContextIdle 计时器，不调用活动读取、目标检查或排序。Loaded 不再启动软件发现；文件夹尺寸变化改为排队刷新，不在弹出布局中重新生成列表。收起时取消计时器并使旧显示结果失效；缓存仅存本机进程内存，不把外部历史保存到磁盘，也不跨完全退出保留。

MainWindow.Refresh 将可见刷新合并为一条流水线。来源读取只更新内存，不直接渲染；随后在界面线程复制设置和数据快照，后台线程完成文件存在检查、快捷方式分类、历史合并和排序，不访问 WPF 或 LibraryStore。结果只在 ContextIdle、窗口可见且显示/搜索代数仍有效时应用。慢读取或失败保留原内容；文件/文件夹及浏览器读取也在等待超时后保留未结束任务，避免重叠。用户主动搜索和编辑仍即时更新，并使旧后台结果失效。

替换 ItemsSource 前比较目标、名称、分类与真实固定项 ID；建议项重新生成随机 ID 不再导致控件重建。内容变化时按目标保留选中项与当前滚动位置。“我的电脑”也复用未变化的卡片，保留展开和滚动状态。主动软件搜索仍复用分类缓存；后台构建使用独立分类实例，避免跨线程共享可变字典。

ShowMenu 的浮层与焦点处理不再依赖 adapter.Enabled，覆盖桌面图标、托盘和二次启动显示路径；窗口隐藏解除 Topmost，失去焦点后收起。焦点失败时最多重试 4 次，每次间隔 60 毫秒，并使用显示代数避免隐藏后的重试把旧窗口拉回。搜索焦点使用 Input 优先级，避免被背景加载拖延。二次启动仅向同路径、同会话的已有 iStart 进程调用 AllowSetForegroundWindow，再发送显示信号，不向任意进程开放。首次启用仍需用户操作底部“启用接入”或设置开关；不自动替换用户选择的原生模式。

InputHookThread 在独立 STA 消息循环安装和卸载低级键鼠钩子，回调仅识别手势、校验本机 HWND 并向 UI Dispatcher 投递；同一待处理打开请求合并，生命周期代数阻止停用前的队列请求在重新启用后执行。UI 心跳超过 2 秒时键盘放行并清理 Win 手势，鼠标后续按下不拦截；已拦截的抬起仍消费，避免半个鼠标手势传给 Explorer。Win10 StartTarget 带真实 ButtonWindow，点击时检查类名、父任务栏、可见性、实时边界；UIAutomation 仍使用 1.5 秒新鲜度限制。主窗口接收 TaskbarCreated、WM_DISPLAYCHANGE、WM_SETTINGCHANGE 后清空快照，并通过快照代数拒绝变化前的扫描覆盖新状态；COM 检测超时尝试纯原生窗口枚举，不累积挂起自动化任务。启用共享同一个待完成任务，有限重试仍失败时保留原生入口。专用线程不注入 Explorer，不添加服务或驱动。

ScreenshotHotkey 在主窗口 HWND 创建后使用 RegisterHotKey（MOD_NOREPEAT）注册全局组合，通过 HwndSource 的 WM_HOTKEY 回调启动截屏；正常关闭注销快捷键并移除 Hook，与开始菜单钩子独立。配置 ScreenshotShortcut 缺失时默认 Alt+Shift+A，未知值不注册；不读取或修改企业微信配置。发生占用时保留菜单截屏按钮并在设置显示冲突，可选择备用组合或关闭。交互验收模式不注册全局快捷键，纯界面测试不安装全局钩子。

截屏由 MainWindow.Screenshot 协调隐藏菜单并等待重绘，Screenshot 使用 GDI CopyFromScreen 获取菜单所在显示器的物理像素；ScreenshotWindow / CaptureSurface 提供浮动工具栏、选区移动缩放和标注。编辑期间阻止开始入口再次显示菜单。导出根据选区裁剪原图与标注，不包含遮罩、边框或工具栏；文字和画笔保留本机内存对象，马赛克按块采样。截图不写入活动历史，不联网；只有用户复制或保存才输出，PNG 编码成功后用临时文件替换目标并清理临时文件。当前仅截取单个显示器；真实桌面隐藏效果、剪贴板和不同 DPI 仍需交互验收。

| 模块 | 已实现职责与边界 |
| --- | --- |
| MainWindow / Refresh / Editing / Shell / Websites / Computer | 五个固定入口、菜单状态缓存、延后后台刷新、搜索、图标/列表、添加与隐藏、同页活动排序、设置、托盘与定位；普通用户运行 |
| LibraryStore | 收藏、分类、偏好、最近记录；复制后修改，临时写入与原子替换，保留一份上一版本；失败不替换内存 |
| Localization / Strings.en.json | 集中管理产品文字与英文翻译；读取当前用户 Windows 显示语言，WPF 绑定即时更新固定文字，导航与快捷键使用稳定标识 |
| SoftwareDiscovery / ShellShortcut | 异步发现用户与公共开始菜单 `.lnk`；IShellLinkW/IPersistFile Unicode 解析；有效 exe 目标去重，过滤安装维护与失效入口 |
| FreeCadDocuments | 有界只读 FreeCAD 最近文件配置；仅纳入本机有效文件，经 RecentDocuments 合并并沿用外部历史开关 |
| ShellIcon / IconReader / ShellIconConverter | 立即显示嵌入占位图，两个后台 STA 线程、有界队列/缓存、冻结 Shell 图像及界面线程更新；UNC 使用通用图标 |
| StartMenuAdapter / ShellCompatibility / WinInput | 版本策略、UIAutomation/原生 Start 子窗口定位；低级键盘/鼠标钩子处理单独 Win 和开始按钮；组合键保留，原生注入使用自有标记避免递归 |
| NativeMenu | SendInput 发送带标记的 Ctrl+Esc 请求临时原生菜单 |
| UpdateService / GitHubUpdater / UpdateWindow | 共用更新状态、联网检查与调度、校验下载/安装、红点、更新说明及升级偏好 |
| SystemActions | 系统策略/能力判断，锁定、注销、切换用户、睡眠、休眠、关机和重启；固定参数，不使用 /f |
| App | 当前 SID 命名互斥锁与显示/退出事件；已有实例正常退出等待；后台启动与诊断模式；ui-test 使用独立配置并禁用真实开机启动修改 |
| installer / scripts / CI | 中文向导与真实文件安装进度、升级前正常退出、卸载与校验值；QA 安装使用独立 AppId，无生产部署 |

preview.25 活动链路修复：RecentDocuments 共用有界来源收集器，隔离可恢复的快捷方式、跳转列表和应用历史错误；目录读取失败不丢弃已完成的文件结果。MainWindow 仅在内存记录部分失败数量，并显示翻译后的提示；取消和 5 秒等待不变。preview.26 将重复软件刷新合并为同一任务，成功结果复用 30 秒；等待超时后保留尚未结束的原生读取，避免累积并行任务。软件分类在搜索之间缓存，发现完成后失效重建。FrequentFolders 先搜索再限额，在内存合并固定目录的重复写法。不新增配置版本或外部历史写入。

preview.26 取消焦点重试的模拟 Alt/Ctrl；搜索框未获焦时也进行有界重试，内容呈现后补一次聚焦。Windows 仍可拒绝前台权限，不修改前台锁定策略、不连接其他线程输入队列。切换菜单依据原生前台窗口归属，不再依赖 Topmost 标志。弹出尺寸使用逻辑窗口尺寸、最小尺寸与 DPI，并按物理工作区限制，不沿用可能最小化的原生矩形。日志增加显示调用耗时和尺寸，不记录输入文字。单独 Win 松开取消和主动打开原生菜单仍使用带标记的 SendInput，并非移除了所有模拟输入。

ShellIcon 附加属性先显示嵌入占位图；两个后台 STA 线程调用 ShellIconConverter，待处理请求与任务缓存最多 256 项。相同路径共用读取，失败保留占位图；冻结图像通过所属 Dispatcher 返回，更新前检查目标，避免复用控件显示旧项目图标。缓慢 Shell 扩展仍可能占用后台线程，但不阻塞界面布局，也不无限创建线程；“我的电脑”共用此链路。

`--reliability-test <directory>` 不显示窗口、不安装钩子，检查慢图标、界面调度、队列限额、异步图像赋值、发现任务合并、定位与快照恢复。`scripts/Test-MenuWindow.ps1` 在新建的非活动桌面运行窗口循环与核心回归，不调用 SwitchDesktop，子进程结束后关闭测试桌面；子进程拒绝在用户输入桌面运行。该检查不验收真实开始接入或实际搜索焦点。`--probe-menu <directory>` 只读取本会话 iStart 的窗口元数据，不读取标题/内容、不激活窗口或发送输入。

preview.25 活动读取修复：MainWindow 先读取 AppSwitched，同一快照用于软件发现与排序。SoftwareDiscovery 检查最多 2048 条正数活动记录，补充最多 128 个有效本机 EXE，排除网络路径、安装维护及常见辅助进程，按目标去重；已有快捷方式及其参数、工作目录优先保留，不枚举整个硬盘。FreeCadDocuments 共享只读 `%APPDATA%/FreeCAD/user.cfg` 及最多 8 个直接 `v*` 版本目录配置，单配置最多 4 MiB，禁用 DTD/外部解析，逐配置隔离失败。仅取 `Root/BaseApp/Preferences/RecentFiles` 中最多 100 个数字 `MRU` 文本项，保留 MRU 顺序；配置修改时间只是跨来源合并的近似依据，不代表每个文件的精确打开时间。沿用最多 100 个文件、有效本机文件过滤和 ShowWindowsRecentDocuments 开关；Windows Recent 目录缺失时仍读取其他软件历史。源历史不变，外部结果仅留在内存。

进程不注入 Explorer、不修改系统二进制、不替换登录认证、不添加服务或驱动。临时原生打开与默认原生切换是独立操作。

兼容验收工具 `scripts/Test-WindowsVm.ps1` 独立于产品运行路径，只在已识别的 Win10 x64 虚拟机调用既有诊断/回归/交互入口，配置和报告按轮隔离。只读预检可在宿主运行；实际键鼠输入、窗口抢焦点和恢复验证留在虚拟机。VirtualBox 是测试环境依赖，不随 iStart 安装，虚拟机镜像、磁盘、账户凭据和本地报告不进入仓库或发行包。

Appearance 通过 DynamicResource 提供双主题资源；ApplyThemeLayout 移动同一个导航控件，保留搜索框、入口列表和状态。Windows 使用顶部导航、平面六列网格、4 DIP 控件和整条系统操作栏；Google 使用左侧胶囊导航、圆角搜索、24 DIP 内容卡片及五列网格。UniformGrid 顶部对齐，少量项目不拉伸填满空白。MenuIcons.xaml 复用 WPF 按钮，使用原始 Figma SVG 转换的嵌入式 PNG 透明遮罩；Prepare-MenuIcons.cjs 只用于维护，不引入运行时 SVG 或网络依赖。软件/文件图标仍来自真实 Shell 数据。图标操作保留事件、翻译的悬停说明、无障碍名称和焦点提示；普通帮助放入标题悬停说明，空状态、错误和操作反馈仍可见。保留原生标题栏、边框、阴影及系统决定的外窗圆角，不增加透明分层窗口或 Mica。换肤不重启适配器、不改隐私和开机启动；输入框保留 PART_ContentHost。详见[双风格验证](THEME_VALIDATION.md)。

## 本地数据

AdapterRecoveryTest 的取消/合并夹具分别固定 26200 和 26300，避免宿主系统升级使待测等待提前退出；26300 无按钮夹具验证发现失败时不安装钩子，26400 夹具验证未知版本在扫描前被拒绝。正式调用始终读取实际系统版本；启动日志补充系统内部版本，与用户偏好一起区分版本拒绝和显示延迟。

`%LOCALAPPDATA%\iStart\library.json` 当前 schemaVersion=2，支持读取 schema 1 并迁移；未知版本拒绝加载。JSON 结构无效时读 `.bak`，不把未知新版本悄悄换成旧备份。

- Entries：id、target、title、categoryId，数组顺序表示手动排序。
- Categories：id、name，保留旧分类顺序与不可删除的未分类标识，兼容旧数据；当前已移除分类编辑界面。
- Launches：target、title、openedAt、count，最多 100 个唯一目标，只表示成功交给系统启动。
- 偏好：隐藏软件、历史读取、常用/最近顺序、自定义入口、开机启动、截屏快捷键、图标视图、Theme（Windows/Google；缺失或未知值回退 Google）。
- Language：`System` / `zh-CN` / `en`，缺失、空值和未知值回退 `System`，沿用 schema 2。系统模式使用 `GetUserDefaultUILanguage` 读取当前用户显示语言，中文映射简体中文，其他语言回退英文；不修改 Windows 语言或进程区域格式。

界面切换先通过 LibraryStore 保存偏好，再更新语言绑定和当前导航、搜索结果、托盘及系统内容。设置面板保持原控件，保留尚未保存的复选项；不重启 StartMenuAdapter、不重新注册截图快捷键，不改写入口名称、隐私或开机启动。截图快捷键选项的显示文字与持久化标识分离；导航图标按固定 GUID 判断。控制面板的七个标题按类别 ID 本地化，磁盘卷标保持用户原文。外部 Shell 名称、原生对话框、安装向导和 GitHub 发布说明保留其自身语言。详见 [界面语言](LOCALIZATION.md)。

配置仅保存在本机；移除入口不删除源文件。存储层保留旧分类迁移与恢复能力，删除分类时将入口移至未分类标识。商店应用补充 AppsFolder 发现，无有效 Shell 入口的软件不保证发现。当前软件排序与来源限制见[活动视图](ACTIVITY_VIEWS.md)；独立推荐区和分类编辑已移除。

## 接入与恢复

StartMenuAdapter 在后台单次扫描任务栏，缓存 StartButton 边界；钩子回调只检查事件与边界并派发 UI 工作，不读配置或运行耗时扫描。鼠标检查窗口根句柄、实时任务栏矩形与缓存新鲜度。运行期间空结果或检测异常只更新重试状态，不撤销用户偏好或停用键盘钩子；保留的旧快照仍须通过 1.5 秒新鲜度与窗口校验，过期则鼠标透传，成功扫描后自动恢复按钮接入；超时未完成的扫描不会反复新建并行扫描任务。

单独 Win 松开时抑制原生弹出并显示菜单，Win 组合键透传；临时原生请求标记注入避免递归。启用前写 session.active，正常退出删除；普通启动发现遗留标记仍退回原生并提示。明确的 --enable-menu 请求优先于遗留标记，独立 enable 信号将请求交给已有实例，避免只显示窗口却没有重新启用。正常会话结束时先释放接入、清理标记并保留用户偏好；取消会话结束则保持接入。进程退出由 Windows 清理所属钩子，已通过独立测试子进程异常退出的原生恢复检查。

低级钩子可能被 Windows 因回调超时移除，当前尚无可靠失效检测；Explorer 重启、多屏/自动隐藏/缩放、锁屏和睡眠恢复仍待隔离验收。版本白名单不是完整兼容性证明。[微软低级键盘钩子说明](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)

## 账户与电源

账户策略读取当前用户/计算机 Policies，睡眠/休眠查询系统能力，不可用操作不显示。关机与重启固定使用 `/t 0` 且无 `/f`，注销等执行前产品界面确认。当前只测试参数与能力查询，未在日常电脑实际执行破坏会话的操作；不能声称完整账户电源验收通过。

## 安装生命周期

中文欢迎、位置、选项、实际文件进度、完成页；当前账户安装，无管理员提权。默认不创建桌面图标、不开机启动、不接管入口，可选启动和接入。安装成功与菜单接入成功分开；兼容检查在实际用户程序执行，失败保留原生入口。

preview.23 打包限制 `SatelliteResourceLanguages=en;zh-Hans`，英文使用框架中性资源，简体中文使用卫星资源；产品英文翻译仍嵌入程序集。PDB 留在开发构建目录，不复制到发布目录。保留完整 WPF/WinForms 自包含运行环境及 COM、区域格式和恢复能力，不启用不受支持的 WPF 裁剪，不手动删除框架程序集。安装器改为 `lzma2/ultra64` 固实压缩，解压字典约 64 MiB；只影响安装器解压阶段，不增加应用运行期依赖。Publish-Preview 在检查绝对路径位于仓库 artifacts 且不是重解析目录后，仅清理固定 `preview-win-x64` 产物再发布，避免旧资源遗留；CI 在打包后直接运行发布 EXE 的现有回归，再验收隔离安装/覆盖/卸载。见[体积记录](PACKAGE_SIZE.md)。

升级前和卸载前调用自身 `--exit`，等待当前用户实例释放互斥锁，超时中止并提示；不强杀用户程序。卸载删除本次安装程序及开机启动值，保留收藏。QA 构建隔离 AppId，跳过真实进程退出/启动设置，不会关闭正在使用的正式 iStart。

安装器复用 Inno 的取消、错误和回滚机制，但尚未完成磁盘不足、权限拒绝、运行文件占用与中断回退的实机验证；不承诺任意升级故障都能自动恢复完整旧版本。签名尚未实现；应用内升级见下文，正式分发前核对 Inno 商业许可及 .NET 随附许可。

## 验证与发布门槛

SmokeTest 验证数据保护、迁移、顺序、发现过滤、最近记录和 WPF 列表/图标渲染。InputIntegrationTest 是交互桌面专项测试，包含原生基线、Win/按钮/组合键/临时原生/禁用/异常进程恢复。Test-Installer 只接受隔离 QA 安装包与 artifacts 内新目录，验证静默首次、覆盖、卸载。

Windows CI 保存核心结果、布局图、安装校验值及安装结果 7 天，不自动发布正式版本。Win10/其他 Win11、多屏缩放、Explorer 重启及安装失败矩阵未通过前，保持试用与版本限制。完整交付门槛见 [执行计划](EXECUTION_PLAN.md)。

当前系统的真实鼠标、重新定位与完整菜单进程恢复证据见 [实机验收](CURRENT_SYSTEM_VALIDATION.md)；按键人工反馈和会话中断项目仍待完成。

窗口 Closing 默认取消关闭并 Hide 到托盘；显式 RequestExit（菜单、托盘、升级/卸载信号）和系统会话结束允许退出。RuntimeLog 记录生命周期与适配器状态，每份约 64 KiB，仅保留一份旧日志，不记录按键或文件路径。接入修复证据见 [恢复记录](RESIDENT_VALIDATION.md)。

ShowMenu 为用户触发的统一显示入口：恢复 Normal、开启 ShowActivated、临时提升层级并请求 SetForegroundWindow，再聚焦搜索。所有用户触发的菜单显示均保持临时浮层至隐藏，不再依赖是否接入开始按钮。呈现期间避免同步 Deactivated 误隐藏；隐藏时清除 Topmost。后台启动的隐藏在首次 Show 后同步完成，不放在异步接入初始化的末尾。前台权限可能被 Windows 拒绝，日志只记录成功标志，不修改系统焦点锁定策略、不借用其他线程输入队列。

主窗口使用 Manual 初始定位，首次可见启动和重复显示信号均调用 ShowMenu。托盘传入鼠标所在屏幕，键盘/重复打开在显示及激活前记录前台窗口屏幕。PlaceMenu 不依赖 adapter.Enabled，在 Show 前后分别定位，适应移动屏幕后 DPI 的变化；MenuPlacement 按物理工作区限制尺寸，固定左下边缘并保留最多 8 像素间距，不沿用开始按钮的居中坐标或上次拖动位置。SetWindowPos 仅定位、不激活；ShowInBackground 共用定位、禁用激活并同步隐藏。调整只影响菜单窗口位置，不扩大系统接管试用名单。

搜索呈现完成后在本线程设置原生 HWND 焦点，再同步 WPF 键盘与逻辑焦点；焦点重试只做有界直接请求，preview.26 已移除模拟 Alt/Ctrl 后备。菜单位置按所选屏幕物理工作区左下角计算；快捷方式图标读取资源或目标，启动路径不变。

## 历史实现记录

以下记录按对应 preview 版本理解。早期推荐区、分类界面和全部收藏混合视图已被 preview.16 的固定活动视图替代，不代表当前功能；后续版本章节保留其实现与验证边界。

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

## 我的电脑与应用内升级

ComputerItems 使用 DriveInfo 读取分区和就绪本机磁盘容量；映射网络盘只展示入口，不查询远程容量。当前用户 Shell.Application 枚举控制面板类别根目录的最多 16 个入口，仅保留系统和安全、网络和 Internet、硬件和声音、程序、用户账户、外观和个性化、时钟和区域这 7 个类别；按系统类别 ID 过滤，排除轻松使用和所有控制面板项，finally 释放 COM 对象。MainWindow.Computer 在独立异步任务读取，等待预算 5 秒；未结束的任务不会重复创建。视图以紧凑三列显示磁盘容量、回收站、网络及默认展开的控制面板项目，支持名称搜索；系统入口通过 explorer.exe 的单个 ArgumentList 参数打开，不写入个人文件历史、不执行清空回收站等操作。ShellIconConverter 沿用 PIDL 图标能力读取虚拟系统入口。

UpdateService 由 MainWindow 持有，共用版本、下载和安装状态。只有安装版且非测试运行才启动一分钟定时器及网络可用事件订阅；成功检查后间隔六小时，检查/下载失败后等待十五分钟，重新联网可在距上次尝试至少两分钟后请求检查。网络事件仅是提示，实际 HTTP 成功才算连通 GitHub。同一检查/下载共享正在进行的任务；退出取消任务、移除订阅并丢弃迟到结果。GitHubUpdater 只访问固定仓库公开 Releases API，最多 100 个版本、响应 2 MiB、30 秒预算；保持数值版本与预览/正式通道选择、排除草稿、精确 x64 资产名、固定仓库 URL、256 MiB 内大小及 GitHub SHA256 digest 校验。下载预算十五分钟，展示实际字节进度，独立 GUID 目录写 .part；大小和 SHA256 一致后才改为 exe。手动模式关闭升级页取消下载，自动模式继续后台下载；关闭自动升级会取消其后台下载并保留提醒。失败清理本次临时文件；成功后仅清理已知更新缓存中超过七天且非最近两次的内容。

AutomaticUpdates 在 schema 2 保存，缺失时默认 true，设置中立即保存；取消后仍检查并提醒。UpdateWindow 使用共用状态，显示当前 AssemblyInformationalVersion（去掉 SHA 后缀）、目标版本、最多 12,000 字更新说明及进度；过滤已知发布来源尾注。“设置”和升级入口显示红点及辅助文字，打开页面不清除提醒。自动安装要求菜单隐藏、没有所属对话框或截屏、没有退出/会话结束请求，且当前会话空闲至少六十秒；GetLastInputInfo 只读取空闲时间，不记录按键。安装前再次检查大小/SHA256，并重新判断偏好和空闲条件。手动安装需明确点击。统一向安装器传入当前目录、合并后的开机启动任务、/NORESTART 和 /NOCLOSEAPPLICATIONS；自动模式另加 /SILENT（显示进度）、/SP- 和 /AUTOMATICUPDATE=1。生产安装成功后 --background 启动新版，保留原生/自定义菜单偏好。安装器退出而旧程序仍在时可重试，自动重试等待十五分钟。只有存在 unins000.exe 的安装版可执行安装；开发和测试不会后台覆盖。配置位置和 schema 不变，无账户 Token 或本机活动上传；安装包未签名，TLS/哈希不能替代代码签名。

SmokeTest.Updater 和 SmokeTest.AutomaticUpdates 使用隔离 HTTP/状态夹具，验证通道、来源、完整性、取消、断网、自动/手动模式、请求共用、空闲门槛、退出后迟到结果、安装前篡改、安装参数、两处红点、版本对比和中英文双风格布局；不执行夹具安装器。QA 身份验证首次、带自动更新参数覆盖安装和卸载。真实公开版本间升级、生产安装后重启、交互取消及异常回退仍需实测，见[升级说明](UPDATES.md)和[历史系统入口验证](COMPUTER_UPDATE_VALIDATION.md)。

## Windows 兼容试用（preview.18 开发版）

ShellCompatibility 将“允许尝试”与“实机已验证”分开：已知 Win10 19041–19045、Win11 22000/22621/22631/26100/26200/26300 可由用户主动启用；未知版本拒绝接入。版本匹配后仍必须发现有效按钮才能安装键盘和鼠标钩子，不能把版本放行当成成功。

Win10/11 都优先读取任务栏下可见、根祖先匹配的原生 Start 子窗口；未找到时使用 UIAutomation 的 StartButton + Button 类型。原生按钮在点击时重新校验，UIAutomation 保留 1.5 秒新鲜度限制。每条任务栏独立容错；两种方式均检查有限坐标、尺寸及任务栏范围，不依赖本地化“开始”名称、不猜测屏幕角落。组合键、原生入口与异常退出恢复不变。本机 26300 未发现符合条件的原生 Start 子窗口，只读发现仍使用 UIAutomation。

ShellProbe 的共享异步任务最多等待 3 秒，超时不反复创建挂起扫描。设置可以将只读诊断保存为用户选择的文本文件；报告包括应用/系统版本、试用策略、显示器数量、按钮数量及识别来源/边界，不包含用户名、文件/浏览历史或日志全文，不自动上传。命令行 --probe-shell 使用同一读取逻辑。

测试覆盖已知/未知版本策略、负坐标/无效几何、屏幕外不激活的本进程原生 Start 窗口夹具、隐藏/越界排除、原有恢复与布局回归。当前 Win11 26200 的 Win、按钮、Win+R、临时原生、禁用及独立子进程异常退出输入测试通过。Win10 的版本分支和窗口结构夹具并非 Win10 系统验收；实际 Win10、其他 Win11、多屏缩放和 Explorer 重启仍待验证，见 docs/WINDOWS_COMPATIBILITY.md。

## 文档语言结构

根目录文档和 `docs/*.md` 默认使用英文，中文对应页单独放在 `docs/zh-CN/`，保留相同英文文件名。每页提供语言切换，图片和源码路径共用。[文档目录](INDEX.md)区分当前指南与历史验收记录；这只是文档结构，不加入程序负载。
