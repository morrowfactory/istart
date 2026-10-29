[English](../ACTIVITY_VIEWS.md) | [简体中文](ACTIVITY_VIEWS.md)

# 软件、文件夹与网站活动视图

preview.16 引入四个固定入口：我的软件、最近打开、常用文件夹、我的网站；之后加入“我的电脑”，形成当前五个入口。移除软件推荐区域与开关、自定义分类编辑和混合收藏视图；旧 Entries / Categories 保留用于兼容和回退，不再展示旧分类。原推荐偏好不再使用，其他原有偏好及启动记录继续保留。

## 我的软件

最多 30 项。仅显示有活动依据的已安装软件和用户明确添加的软件；按本机活动分值降序、最近 iStart 打开时间、名称排序。Windows 来源为当前用户 `Explorer/FeatureUsage/AppSwitched` 的正整数累计切换次数，按 exe 精确路径或 `System.AppUserModel.ID` 匹配；已知 Program Files GUID 展开为系统目录。和 iStart 启动次数取较大值作为排序参考，不相加伪装成精确启动次数，不声称一个月统计。读取上限 2048 个注册表值，失败时使用已有入口和 iStart 记录。

搜索可查找尚无使用记录的已安装应用，仍最多 30 项。手动添加的软件可在本页显示；“隐藏”只加入本机 HiddenSoftware 列表，不卸载软件，设置中可重置。软件路径、应用列表和活动记录不上传。

preview.25 活动修复：检查同一 Windows 快照中最多 2048 条正数活动记录，补充最多 128 个有效本机 EXE 候选；失效旧路径、已发现软件和被过滤的入口不占补充名额，使没有开始菜单快捷方式的本机便携程序也能显示。排除失效、相对、网络路径、安装维护与常见辅助进程；已有快捷方式保留原启动参数。切回“我的软件”会刷新发现与活动；被新任务替代的刷新会取消，不覆盖新任务的状态。不扫描硬盘，仍最多显示 30 个软件。

## 最近打开：FreeCAD 来源（preview.25）

FreeCAD 不一定将打开文件写入 Windows Recent。iStart 只读当前用户 `%APPDATA%` 下的 `FreeCAD/user.cfg` 及最多 8 个直接 `FreeCAD/v*/user.cfg` 版本配置，每个最多 4 MiB。仅解析 `Root/BaseApp/Preferences/RecentFiles/FCText` 中名称为 `MRU` 加数字的项，按数字排序，单配置最多 100 项。支持 STP、STEP、FCStd 等有效本机文件；排除目录、失效文件、程序、相对和网络路径，不读取材质等其他 `Recent` 分组。

MRU0 为最近保存的第一项；`RecentFiles` 是显示容量设置，不是访问次数。这里没有各文件的打开时间，配置修改时间仅作跨来源合并的近似依据。不监控尚未写入配置的会话变化或自定义 `--user-cfg` 位置。沿用现有外部文件历史开关，不读取文件内容、不修改配置、不持久化或上传外部记录。Windows Recent 目录不存在也不阻断此来源。

2026-10-29 本机检查发现 4 条有效 FreeCAD 文件记录，其中 1 条 STP，且存在有使用依据但无当前开始菜单快捷方式的 FreeCAD EXE。样本验证覆盖数字顺序、旧版/版本目录合并、源配置保留、过滤、取消、损坏/超限 XML 与 DTD 拒绝；既有回归检查通过。这是补齐已知来源，不承诺获取所有软件的完整历史。

依据：[FreeCAD 最近文件读写实现](https://github.com/FreeCAD/FreeCAD/blob/main/src/Gui/Action.cpp)。

## 读取容错与文件夹搜索（preview.25）

Windows Recent 快捷方式、自动跳转列表、LibreOffice、WPS 和 FreeCAD 分别读取；一个来源出现可恢复错误，不丢掉其他成功来源。目录历史读取失败也保留已完成的文件读取；两种界面语言均显示部分读取提示。取消继续向上传递，沿用界面 5 秒预算；不保证整个操作超时时仍能返回部分结果。

常用和最近两种文件夹模式都先按名称/路径搜索，再限制结果数量；手动命名的目录也可搜索。固定目录的大小写、尾斜杠重复写法会合并，不重写保存的入口或源目录。

2026-10-29 检查发现软件候选为 145 个，修复失效路径占额前为 114 个；“我的软件”仍最多显示 30 个。各来源有效记录为 LibreOffice 17 条、WPS 33 条、自动跳转列表 50 条、FreeCAD 4 条，本轮未发生读取失败。这些来源可能重叠，且最终仍最多合并 100 个文件。回归覆盖失效路径挤占、来源锁定/失败、取消、搜索超出可见名额的文件夹、目录重复写法和反复软件刷新；不承诺覆盖未支持软件或尚未保存的历史。

## 常用文件夹

默认按 iStart 目录打开次数和近期文件目录活动排序，同分按最近时间排序；自动补充空位。页面按钮切换为最近打开，改用直接指向文件夹的自身/Windows 记录，不把文件的父目录视为一次直接目录打开。外部时间可能近似，不代表 Explorer 的完整访问计数。已有固定目录继续作为候选，不改写持久化顺序；无直接打开记录的固定项不会进入最近模式。

## 我的网站

用户已确认 Chrome 与 Edge 合并，按域名合为一项；同页按钮在常用/最近排序间切换并保存。两种模式最多 30 个网站。汇总浏览器保留的累计 URL 访问次数，最近模式按最后访问时间；主机名忽略大小写，非默认端口分开，HTTP/HTTPS 默认端口合并。显示最后访问页面的标题，打开网站根地址，不保留查询参数。两浏览器可能有同步重叠，iStart 打开记录也可能重叠，因此排序依据不能当作精确访问审计。

只支持 HTTP/HTTPS，过滤 hidden 非零、无访问次数、本机文件、内部协议与带用户凭据的地址。不联网获取 favicon，使用本地网站图标；不读取 Cookie、密码、网页内容或无痕会话。设置中可关闭浏览器历史读取，关闭后清空外部内存缓存；仅显示通过 iStart 打开的网站。“清除 iStart 记录”不会删除浏览器历史。

读取 `%LOCALAPPDATA%/Google/Chrome/User Data` 与 `Microsoft/Edge/User Data` 下 Default / Profile 配置，单浏览器最多 10 个现存历史库。浏览器运行时可能锁库，因此复制 History 及存在的 WAL/SHM 到随机私有临时目录，使用系统 winsqlite3 以只读方式打开快照，finally 关闭句柄并在校验临时目录边界后删除。每文件最多 128 MiB，每配置最多读取最近 10000 个 URL，整个 UI 读取限时 5 秒；不关闭浏览器、不写源数据库。它是有限历史快照，不承诺同步瞬间的完整历史。失败/部分失败/超时明确提示，不能当成没有历史。

### 已确认字段目录

来源对象为 Chrome / Edge 的 SQLite `urls` 表，扁平记录，无业务枚举或自定义字段。两者本机只读样本的 PRAGMA 列一致，共七列；不将其他 History 表当作已完成集成。代码仅使用 url、title、visit_count、last_visit_time、hidden。代表值已脱敏。

| 字段/API 路径 | 样本类型 | 含义/使用 | 示例 | 可空性与证据 |
| --- | --- | --- | --- | --- |
| urls.id | INTEGER / PK | URL 行标识；不使用 | 整数（已遮蔽） | 主键；本机 PRAGMA 与 Chromium 定义 |
| urls.url | LONGVARCHAR | 地址；仅 HTTP/HTTPS | https://[域名已遮蔽]/... | PRAGMA 未设 NOT NULL；空值过滤 |
| urls.title | LONGVARCHAR | 最后页面标题 | [标题已遮蔽] | 可空；空标题回退域名 |
| urls.visit_count | INTEGER | URL 累计访问次数；汇总排序 | 323 | NOT NULL；样本和 Chromium 定义 |
| urls.typed_count | INTEGER | 输入型访问计数；不使用 | 整数（未用于映射） | NOT NULL；本机 PRAGMA |
| urls.last_visit_time | INTEGER | Chromium 时间值，转为 UTC 最后访问时间 | 转换后 2026-10-12 08:59:56 UTC | NOT NULL；异常时间过滤；样本转换检查 |
| urls.hidden | INTEGER | 只纳入 0；其他值过滤 | 0 | NOT NULL；本机样本和 Chromium 查询 |

这七列的映射和按域名合并方式已由用户在当前会话确认。实现使用动态查询检查字段可读性，不承诺未来浏览器内部结构不变；结构不支持则报告失败。

依据：[Chromium URL 表源码](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/url_database.cc)、[Microsoft 系统 SQLite API 清单](https://learn.microsoft.com/en-us/uwp/win32-and-com/win32-extension-apis)、[SQLite 只读打开标志](https://sqlite.org/c3ref/c_open_autoproxy.html)。实际本机 PRAGMA 与少量只读样本用于确认当前配置，未提交真实历史。

## 验证

夹具检查软件排序/30 项限制/隐藏/无活动不推荐，双浏览器目录过滤、域名合并、常用与最近排序、网址搜索、临时隐藏和损坏历史报告；界面验证四入口、移除控件、同页切换及偏好保存、双主题布局。`--probe-activity <输出目录>` 只输出来源数量，不输出网址、标题或软件路径；可用于当前电脑的只读来源诊断。
