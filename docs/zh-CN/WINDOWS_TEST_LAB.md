[English](../WINDOWS_TEST_LAB.md) | [简体中文](WINDOWS_TEST_LAB.md)

# Windows 虚拟机兼容测试

2026-10-16 准备状态快照：Win10 已安装并进入过桌面，应用自动验收尚未执行，不能据此声明已兼容。本轮虚拟机测试基线固定为 `0.2.0-preview.21`，来源 `cb445edcdf758debc5a139b75b9d8632414e827a`；不是最新公开版本的声明。

2026-10-16 本机准备记录：VirtualBox 7.2.20 已安装且基础虚拟机启动成功，无需重启宿主；已创建 4 核、8 GiB、80 GiB 动态磁盘的专用测试机。官方中文评估镜像已下载并通过微软 SHA256 校验，Win10 已完成安装并进入测试账户桌面，Guest Additions 驱动日志确认系统 build 19045；远程执行服务仍在检查，应用验收尚未运行。测试包已校验并打包为只读光盘。脚本已在 Windows PowerShell 5.1 完成只读预检，确认 Win11 宿主会被交互测试防护拒绝。

记录时本机后台流程已启动，当时等待 Windows 正常关机收尾后重新启动，再检查远程执行服务、保存系统快照、挂载测试光盘并启动普通权限验收。状态与错误写入工作区旁的 `.istart-testlab/setup-state.json` 和 `setup-error.txt`；实际结果导出至 `.istart-testlab/win10-results`。Windows 许可状态未就绪时停止应用验收并记录前置检查失败，不将准备失败计为产品兼容失败。快照和应用测试尚未完成；限时等待失败会保存现场。这是当时状态的记录，不表示该后台流程现在仍在运行。

## 环境与隔离

- 当前 Win11 电脑作为宿主；Win10 22H2 x64 虚拟机作为首要目标，后续增加 LTSC 2021（19044）。同一虚拟机无法替代不同厂商硬件实测。
- 推荐 4 核、8 GiB 内存、80 GiB 动态磁盘，VirtualBox 的基础 NAT 网络；不安装 Extension Pack，不启用桥接网络、共享剪贴板或拖放，不共享个人目录。
- 使用微软官方评估镜像或有相应授权的安装镜像。评估系统遵循镜像内许可及有效期，不延长或绕过评估期限；Host 的 Windows 授权不能直接视作虚拟机授权。
- 测试机使用专用本地账户，不导入真实配置、文件、浏览历史或账号。公开安装包与测试脚本通过只读光盘镜像传入。
- 在系统及 Guest Additions 安装完成后创建 `clean-os` 快照；公开基线安装后创建 `istart-baseline`。保留这两个恢复点及正在调查问题的快照；无默认批量清理。

## 安装包基线

从 [preview.21 发布页](https://github.com/morrowfactory/istart/releases/tag/v0.2.0-preview.21)下载安装包和 `release-manifest.json`，核对版本、来源 SHA、文件长度和 SHA256。

本轮安装包：`iStart-Setup-0.2.0-preview.21.exe`，53,635,225 字节，SHA256：

```text
2734c997d0ffb8533da87c4b9f9aeb4ac86723316e308e64f06b63850492d23e
```

Win10 22H2 中文 x64 Enterprise Evaluation 镜像校验值来自[微软校验文档](https://download.microsoft.com/download/c/1/1/c11d2ca5-967c-45c0-bc7d-2d9ca3f1fe07/Windows10Enterprise22H2HashValues.pdf)：

```text
B586B4A2B936B44CEC5A785E0E2696B4816A66A40D287D4203CE18F56970D1C4
```

## 自动测试

在虚拟机安装公开基线后，从托盘退出日常 iStart，保持虚拟机登录和桌面解锁，再执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-WindowsVm.ps1 -Rounds 3
```

脚本拒绝在宿主机、非 Win10 19044/19045 x64 虚拟机、管理员或服务会话中注入输入。测试通过交互会话的普通权限运行，避免管理员权限掩盖问题。`-PreflightOnly` 在任何 Windows 中只记录环境和程序版本，不接入开始菜单。

自动部分使用程序既有验收入口，依次执行按钮发现、数据/布局回归、Win/开始按钮/Win+R/原生切换/测试子进程异常退出恢复，以及最大化背景下置前、搜索焦点、失焦收起、最小化恢复和实际启用接入。配置和输出位于专用测试目录；不改日常 iStart 设置。测试输出保留在虚拟机桌面的 `iStart-test-results`，每次新建目录。自动通过仍显示 `AutomatedPass-ManualPending`。

`-Rounds` 只是重复既有窗口测试，每轮启动新的测试进程，不能代表同一实例长时间稳定运行。脚本未在 Win10 执行前，只能记录语法检查及宿主预检结果。

## 人工矩阵与通过门槛

| 场景 | 操作与验收 |
| --- | --- |
| 用户报告的两个问题 | 最大化记事本后，分别按 Win、点击系统开始按钮、点击 iStart 快捷方式；菜单置前，搜索可直接输入英文及中文，原生菜单不同时出现 |
| 连续使用 | 同一实例交替 Win/开始按钮各 50 次，空闲 45 分钟后重复 10 次；不丢接入、不被遮挡、不重复弹出 |
| 原生与组合键 | Win+R、Win+E 正常；临时原生入口、停用、退出均恢复原生菜单 |
| 任务栏 | 自动隐藏开/关，底部及左/右/顶部逐项验证；不猜测坐标，识别失败保持原生 |
| 显示 | 100%、150%、200% 缩放分别测试；切换分辨率后重试；多屏另行标记，不以单屏替代 |
| 恢复 | 仅在虚拟机重启 Explorer、锁屏解锁、睡眠唤醒及重启登录后验证；不可对宿主机执行这组操作 |
| 安装维护 | 首次安装默认原生；主动启用后可接入；升级保留测试配置；卸载后原生可用 |

每项记录系统版本/补丁、iStart 来源 SHA、显示设置、步骤、实际结果以及 Pass/Fail/NotRun。截图只包含测试数据。任一核心项失败先保存日志并修复，再复测同场景和已通过核心项。Win10 22H2 与 LTSC 结果分别记录，不合并为“所有 Win10 已通过”。

GitHub `windows-latest` 当前是 Windows Server 镜像，现有 CI 不能代替桌面 Start 菜单验证，见 [GitHub 镜像说明](https://github.com/actions/runner-images#available-images)。
