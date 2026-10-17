[English](../THEME_VALIDATION.md) | [简体中文](THEME_VALIDATION.md)

# Windows / Google 双风格验证

## preview.24 开发版：两套独立紧凑布局

日期：2026-10-17。按已批准的 [Windows 设计](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=49-747)和 [Google 设计](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=49-1000)适配现有 WPF 控件。Windows 使用顶部导航、中性平面六列网格和底部系统栏；Google 使用左侧胶囊导航、白色圆角卡片和五列网格。次要操作使用本地图标、翻译的悬停说明和无障碍名称；导航、软件名及展开的账户/电源选项保留文字。搜索、排序、添加、截屏、原生恢复和升级红点保留原有事件。

六/五列位置、本地图标、默认及紧凑英文边界、语言切换、状态保留和升级红点检查通过。使用十二个不执行的测试软件入口生成 WPF 图片，对照设计检查。原始 SVG 与衍生 PNG 均在本地，真实软件图标仍动态提取。为保留置前兼容性，标题栏和外窗圆角仍由 Windows 决定；Figma 字体适配为已安装的 Segoe UI / Microsoft YaHei UI。

实际程序保留原有 740×660 DIP 窗口容量，避免增加“我的电脑”和真实软件清单的滚动；内容区随窗口伸展。较小的设计参考渲染用于紧凑尺寸检查。这是开发验收，尚未公开发布 preview.24，也不是 Win10 实机通过。真实拖入、其他 DPI/显示器及完整账户/电源行为不在本次布局验收范围。下面保留早期里程碑记录。

版本：0.2.0-preview.5；日期：2026-10-13；当前 Windows 11 26200.9457。

## 使用

打开设置，在“外观风格”下拉框选择 Windows 风格或 Google 风格。选择后立即应用并保存，无需点击下方的菜单设置保存按钮。Windows 风格使用浅灰背景、小圆角、描边按钮、选中指示条和四格图案；Google 风格保留圆润的 Material 视觉。

主题仅改变外观，不改变开始按钮/Win 接入、开机启动、推荐/记录选项、收藏内容或排序。新增可选 Theme 字段，仍为 schema 2；旧配置或未知主题回退 Google。

## 验证

- Release 构建：0 警告、0 错误。
- 调用设置共用的 SetTheme：Google→Windows→Google 即时更新同一窗口，实际输入框模板圆角随之更新。
- Windows 与 Google 分别保存后重新读取，选择一致；收藏顺序与自定义接入偏好保持不变。
- 旧配置默认 Google，未知值回退 Google，不丢弃收藏。
- 两种风格实际 WPF 渲染已检查：[Google](../prototypes/theme-google.png)、[Windows](../prototypes/theme-windows.png)；已有图标/紧凑布局及边界检查通过。
- 核心数据保护、接入扫描恢复、关闭收起/显式退出检查继续通过。
- 安装生命周期与最终自包含程序通过本版打包和 CI 检查；验收日志保留在 artifacts 内。

实机下拉框点击尚未完成：Windows 交互工具报告“window id 5642440 no longer belongs to …; current owner is …”，前后路径相同；重新选择窗口后仍失败，已停止输入并仅结束本次独立验收进程。没有把代码检查冒充实际鼠标点击通过。未触碰个人收藏或接入状态。

多屏/其他 DPI、Win10/其他 Win11、长期钩子健康和安装异常矩阵沿用既有限制；安装包未签名。

README、ARCHITECTURE 和执行计划已同步；VISION 已复核，产品方向与隐私原则不变。

本文记录 preview.5 的早期样式，Windows 外观后来在 [preview.13](WIN11_FLUENT_VALIDATION.md) 继续优化。
