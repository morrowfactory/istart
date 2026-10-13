# Windows / Google 双风格验证

版本：0.2.0-preview.5；日期：2026-10-13；当前 Windows 11 26200.9457。

## 使用

打开设置，在“外观风格”下拉框选择 Windows 风格或 Google 风格。选择后立即应用并保存，无需点击下方的菜单设置保存按钮。Windows 风格使用浅灰背景、小圆角、描边按钮、选中指示条和四格图案；Google 风格保留圆润的 Material 视觉。

主题仅改变外观，不改变开始按钮/Win 接入、开机启动、推荐/记录选项、收藏内容或排序。新增可选 Theme 字段，仍为 schema 2；旧配置或未知主题回退 Google。

## 验证

- Release 构建：0 警告、0 错误。
- 调用设置共用的 SetTheme：Google→Windows→Google 即时更新同一窗口，实际输入框模板圆角随之更新。
- Windows 与 Google 分别保存后重新读取，选择一致；收藏顺序与自定义接入偏好保持不变。
- 旧配置默认 Google，未知值回退 Google，不丢弃收藏。
- 两种风格实际 WPF 渲染已检查：[Google](prototypes/theme-google.png)、[Windows](prototypes/theme-windows.png)；已有图标/紧凑布局及边界检查通过。
- 核心数据保护、接入扫描恢复、关闭收起/显式退出检查继续通过。
- 安装生命周期与最终自包含程序通过本版打包和 CI 检查；验收日志保留在 artifacts 内。

实机下拉框点击尚未完成：Windows 交互工具报告“window id 5642440 no longer belongs to …; current owner is …”，前后路径相同；重新选择窗口后仍失败，已停止输入并仅结束本次独立验收进程。没有把代码检查冒充实际鼠标点击通过。未触碰个人收藏或接入状态。

多屏/其他 DPI、Win10/其他 Win11、长期钩子健康和安装异常矩阵沿用既有限制；安装包未签名。

README、ARCHITECTURE 和执行计划已同步；VISION 已复核，产品方向与隐私原则不变。
