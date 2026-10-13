# 最近文档：preview.9

日期：2026-10-13。

旧实现只显示 library.json 中的 iStart Launches，其他软件打开的文件不会写入，所以空列表并不代表程序读取了系统历史。

新增读取当前用户 Windows Recent 文件夹；最新 200 个 .lnk 中筛选存在的本机文档，合并 iStart 记录后按时间去重至最多 100 项。选择最近视图和再次显示菜单时刷新；异步等待三秒，失败保留 iStart 记录并提示。Windows 快捷方式修改时间作为排序依据，不宣称精确打开时间。

设置增加“显示 Windows 最近文档”，不依赖 iStart 启动记录开关；关闭即清空内存结果，不改系统设置。不读文件内容、不上传、不扫描全盘、不读取应用私有历史或 Jump List。Windows 文档结果不持久化进 iStart 配置；清除 iStart 记录不会删除系统历史。

本机只读检查：Windows Recent 根目录文档快捷方式 0 条，iStart Launches 0 条，因此当前不能展示之前全部文件，也不能冒充历史恢复成功。用户需通过 iStart 打开文件，或由 Windows 提供 Recent 记录后验证。

夹具检查：有效中文文档、重复快捷方式、不存在目标、文件夹过滤；合并保留较新记录并去重；关闭开关保存重开。构建及核心回归通过。README、ARCHITECTURE、执行计划同步；VISION 复核，方向不变。

## preview.10 跨软件历史扩展

用户明确要求其他软件打开的文件，包括 WPS、LibreOffice、图片。preview.9 的空状态只适用于 Recent 根目录和 iStart 两种来源，不代表系统所有历史为空。

新增只读 AutomaticDestinations 的 OLE Shell Link 流（不解析或修改 DestList）、LibreOffice 的 PickList/OrderList/HistoryItemRef，以及 WPS 6.0 组件 RecentFiles/files 数值子键 path。样本仅确认字段类型与扩展名：WPS 文档 path 为本机 .doc，表格为 .xlsx，PDF 为 .pdf；LibreOffice 为 file URI。关联账户/设备不读取，不输出用户文件名或路径。

本机来源计数：Jump List 有效 51 条，含 13 图片、20 办公文档；LibreOffice 有效 24 条；WPS 有效 31 条；合并去重上限 100 条，含 13 图片、45 办公文档。计数随用户操作和文件存在情况变化；不能据此保证全部软件历史完整。不存在或网络目标、目录和可执行/脚本过滤。不需要通过 iStart 打开才展示。

Jump List 用流/容器修改时间近似排序；LibreOffice 使用软件序号及配置修改时间；WPS 无确认的打开时间，保持序号并置于有时间记录之后。暂不读取 CustomDestinations、WPS 云历史或无已知来源的应用私有数据库。仅手动打开最近视图/再次呈现时刷新，不实时监控全系统文件打开。

LibreOffice URI 夹具、Recent 去重/过滤、偏好保存与原有核心回归通过；当前机器真实历史读取在五秒预算内完成。COM 资源释放、XML 禁用 DTD/外部解析；读取操作未执行历史目标或修改源记录。VISION 复核，仍符合本机、透明、可关闭的原则。

参考：[微软 IStorage::OpenStream](https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-istorage-openstream)、[LibreOffice 配置解析](https://docs.libreoffice.org/configmgr.html)。
