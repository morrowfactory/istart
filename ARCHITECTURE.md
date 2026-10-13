# iStart 架构说明

## 状态与约束

当前使用 .NET 10、C# + WPF，单一桌面项目，无额外 NuGet 依赖；Windows Forms 仅提供托盘和显示器/睡眠能力。Inno Setup 7.1.0 构建当前用户的中文自包含安装包。目标是 Win10/11 x64，开始入口现仅允许内部版本 26200 试用，其余版本保持原生模式。实际证据见 [验证记录](docs/P0_VALIDATION.md)。

## 模块边界

| 模块 | 已实现职责与边界 |
| --- | --- |
| MainWindow / Editing / Shell | 分类、搜索、图标/列表、拖入排序、右键管理、撤销、设置、托盘与定位；普通用户运行 |
| LibraryStore | 收藏、分类、偏好、最近记录；复制后修改，临时写入与原子替换，保留一份上一版本；失败不替换内存 |
| SoftwareDiscovery / ShellShortcut | 异步发现用户与公共开始菜单 `.lnk`；IShellLinkW/IPersistFile Unicode 解析；有效 exe 目标去重，过滤安装维护与失效入口 |
| ShellIconConverter | Shell 图标提取与有界缓存；UNC 使用通用图标，避免远程图标读取 |
| StartMenuAdapter / WinInput | UIAutomation 定位 StartButton；低级键盘/鼠标钩子处理单独 Win 和开始按钮；组合键保留，原生注入使用自有标记避免递归 |
| NativeMenu | SendInput 发送带标记的 Ctrl+Esc 请求临时原生菜单 |
| SystemActions | 系统策略/能力判断，锁定、注销、切换用户、睡眠、休眠、关机和重启；固定参数，不使用 /f |
| App | 当前 SID 命名互斥锁与显示/退出事件；已有实例正常退出等待；后台启动与诊断模式；ui-test 使用独立配置并禁用真实开机启动修改 |
| installer / scripts / CI | 中文向导与真实文件安装进度、升级前正常退出、卸载与校验值；QA 安装使用独立 AppId，无生产部署 |

进程不注入 Explorer、不修改系统二进制、不替换登录认证、不添加服务或驱动。临时原生打开与默认原生切换是独立操作。

界面资源在 App.xaml 统一定义圆角按钮、输入框和列表项状态；MainWindow.xaml 负责 Material 风格的品牌搜索区、分类栏、白色内容卡片和底部系统入口。继续使用 WPF 与 Microsoft YaHei UI、本机 Shell 图标，无额外 UI 库。输入框保留 PART_ContentHost、列表项保留拖入边框和键盘焦点提示；配置格式、系统接入与恢复路径不因换肤改变。设计来源和实测见 [界面验证记录](docs/MATERIAL_VALIDATION.md)。

## 本地数据

`%LOCALAPPDATA%\iStart\library.json` 当前 schemaVersion=2，支持读取 schema 1 并迁移；未知版本拒绝加载。JSON 结构无效时读 `.bak`，不把未知新版本悄悄换成旧备份。

- Entries：id、target、title、categoryId，数组顺序表示手动排序。
- Categories：id、name，数组顺序表示分类排序，未分类不可删除。
- Launches：target、title、openedAt、count，最多 100 个唯一目标，只表示成功交给系统启动。
- 偏好：推荐开关与排除项、记录开关、自定义入口、开机启动、图标视图。

配置仅保存在本机；移除入口不删除源文件，删除分类将收藏移至未分类。手动排序与推荐排序独立。推荐候选不持久化，最多显示 12 项；可靠系统历史尚无来源，不读取 UserAssist 或宣称完整使用频率。Store 应用与无快捷方式软件不保证发现。

## 接入与恢复

StartMenuAdapter 在后台单次扫描任务栏，缓存 StartButton 边界；钩子回调只检查事件与边界并派发 UI 工作，不读配置或运行耗时扫描。鼠标检查窗口根句柄、实时任务栏矩形与缓存新鲜度。扫描失败停用接入；超时未完成的扫描不会反复新建并行扫描任务。

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
