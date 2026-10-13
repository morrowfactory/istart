# 搜索修复验证：preview.7

2026-10-13。截图输入 codex 后推荐区不变：旧版 TextChanged 仅筛选收藏，推荐前 12 项没有参与搜索；同时只发现传统 exe 快捷方式，漏掉商店应用。

当前实现：即时搜索全部已发现软件和跨分类收藏，软件结果滚动展示；无结果有说明。名称和入口标识均忽略大小写匹配。关闭推荐保留本地软件搜索，不扫描全盘文件、不联网上传。商店应用暂不支持固定与拖入，支持交给 Windows 打开。

本机只读样本：AppsFolder 显示名 ChatGPT，标识 OpenAI.Codex_2p2nqsd0c76g0!App。程序发现 166 个候选、Codex 标识 1 个。回归检查覆盖软件第 13 项以后、大小写、无结果、关闭推荐后搜索、显示名不同的商店标识。构建 0 警告、0 错误。测试未自动输入系统按键或启动 Codex。

README、ARCHITECTURE、执行计划同步；VISION 复核，方向未变。界面实际搜索与点击启动仍需用户反馈，不以只读发现冒充启动验收。

## preview.8 商店图标

用户截图确认商店结果只有文字、图标空白。改为解析 Shell PIDL 后读取原生图标；失败回退通用应用图标。HICON 和 PIDL 均释放，冻结位图沿用有界缓存。本机实际 Codex 32×32 图标保存为测试输出 codex-icon.png，白底预览确认原应用标志；构建与核心回归通过。VISION 复核，方向不变。

API 依据：[微软 SHGetFileInfo 文档](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)。

preview.11：推荐默认关闭，整个容器收起并无占位；菜单内不再有启用/关闭按钮，设置为唯一开启入口。正常推荐模式也隐藏标题/说明；搜索时才显示搜索结果标题。默认值、关闭时搜索可见、清空时容器与标题收起回归通过。README/ARCHITECTURE/执行计划同步；VISION 复核无方向变化。
