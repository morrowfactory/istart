# 安装包体积优化

preview.23 是安装体积优化开发版，尚未公开发布；公开下载仍为 preview.22。菜单、托盘、截屏、活动读取、升级和系统恢复功能保持原实现。

## 本地对比

2026-10-16，在同一台 Windows 11 机器使用 .NET SDK 10.0.401、Inno Setup 7.1.0，对最新 main `62ca6c2` 的原始打包方式与优化工作区各构建一次。以下为该轮样本，后续提交的版本元数据可能带来少量字节差异。

| 项目 | 优化前 | 优化后 | 减少 |
| --- | ---: | ---: | ---: |
| 安装包 | 53,610,564 字节（53.6 MB） | 50,929,534 字节（50.9 MB） | 2.7 MB，约 5.0% |
| 自包含发布目录 | 179,900,614 字节（179.9 MB） | 163,224,290 字节（163.2 MB） | 16.7 MB，约 9.3% |
| 发布文件数 | 473 | 268 | 205 |
| 框架语言目录 | 13 | 1（zh-Hans） | 12 |
| PDB 调试符号 | 1 | 0 | 1 |

MB 按十进制计量。主程序 `IStart.App.dll` 约 0.37 MB；安装包主要包含离线运行所需的 .NET/WPF/WinForms 框架。删除源码注释或 using 不会明显缩小安装包，现有诊断与回归入口继续保留。

## 已实施的打包设置

- `SatelliteResourceLanguages=en;zh-Hans`：产品当前只提供英文和简体中文。英文来自框架中性资源，产品英文翻译继续嵌入程序集；只附带简体中文框架卫星资源，其他系统语言按原有策略显示英文。原生 Windows 对话框仍遵循系统本身的语言。
- `CopyOutputSymbolsToPublishDirectory=false`：PDB 保留在开发构建输出，不随安装包分发。运行诊断日志和恢复机制保留。
- `Compression=lzma2/ultra64` 与 `SolidCompression=yes`：增加构建压缩工作量，安装解压字典约 64 MiB；程序运行时无需额外组件。
- 发布前只清理经过路径范围检查的固定 `artifacts/preview-win-x64` 生成目录，拒绝该目录为重解析点的情况，防止重复发布夹带旧语言资源和调试文件。其他安装包、个人配置及测试结果不在清理范围。

继续随包提供完整的自包含运行环境和许可文件。WPF、WinForms、动态 COM 与 Shell 接入均保留；不启用 WPF 不支持的程序集裁剪，不手动删除可能被反射或系统互操作加载的框架 DLL，不改变日期/区域格式能力。后续增加产品语言时，需要同步扩展发布语言列表并重新验证。

旧版本已安装的语言资源不由本次升级主动删除；表中的目录体积表示新发布内容和全新安装的负载，不能当作旧安装目录的自动清理结果。

## 验证与交付

沿用 `scripts/Test-Prototype.ps1` 检查核心功能、中英文界面、布局、截屏、升级校验及恢复模型。CI 新增直接运行自包含发布 EXE 的同一回归，确保开发机共享框架不会掩盖发布资源缺失；两类结果一起保存到技术验证产物。QA 安装器继续检查中文空格路径下的首次安装、覆盖和卸载。

本轮 Windows 11 本地构建为 0 警告、0 错误；开发构建与自包含 EXE 的现有回归通过，优化 QA 包的首次安装、覆盖及卸载通过。实际产物只包含 zh-Hans 框架语言目录，无 PDB 文件。VISION.md 已核对：本次仅改变打包效率，无需调整产品方向或功能范围。

构建参数和路径使用 README 中的现有命令。公开发布仍必须通过最新 main 的成功 GitHub Actions 构建及发布工作流；本地体积验证安装包不能替代正式发布产物。Win10 和其他 Win11 版本的实机验收范围保持原有说明。

参考：[.NET 发布语言资源属性](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#satelliteresourcelanguages)、[WPF/WinForms 裁剪限制](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities)、[Inno 压缩等级与内存要求](https://jrsoftware.org/ishelp/topic_setup_compression.htm)。
