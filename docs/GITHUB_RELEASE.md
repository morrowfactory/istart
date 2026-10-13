# GitHub 预览版发布

安装包发布到本仓库 GitHub Releases；无需远程服务器部署。先合并 PR，再使用 main 的成功构建产物，禁止上传本机安装包替代 CI 产物。

1. 获取最新 origin/main 完整 SHA，等待该 SHA 的 build.yml push/main 构建成功。构建验证 DLL ProductVersion 包含当前检出 SHA，并完成核心、布局及隔离安装/覆盖/卸载检查。
2. 手动运行 release.yml，输入 release_sha、build_run_id、明确发布授权记录 approval_note。流程在检出前验证最新 main 与构建来源，在公开前再次检查。
3. 只允许正式 iStart 安装器和同名 SHA256 文件；拒绝 QA 包、多余文件或哈希不符。版本从项目读取，仅支持 preview 版本。
4. 创建草稿、上传安装包/校验/manifest，再公开为 prerelease，不设置稳定最新版。随后重新下载公开资产校验，并核对标签提交及 release-manifest.json。
5. 失败时保留草稿和证据，不公开不完整产物。已存在的版本/标签不覆盖；修复后通过新 PR 和新版本发布。若公开后验收失败，先撤回公开状态并通过修复 PR 或明确批准的回退流程处理，不覆盖同名资产。

流程使用 GitHub Token 的 contents:write / actions:read，设置 github-release 环境和串行 concurrency；不需要额外服务器密钥。当前 main 没有分支保护，github-release 没有已核实的强制审批规则。本次 PR #1 与 v0.2.0-preview.13 由用户在会话明确授权“合并 发布”；发布记录如实保存授权、保护状态和精确 SHA，不声称 GitHub 已强制审批。以后的发布仍需明确授权和全部检查。

发布数据只包括安装包、SHA256、release-manifest.json（来源 SHA、构建编号、安装器哈希和授权记录），不包括用户收藏、运行日志、历史样本、QA 包或本机备份。

GitHub Actions 临时产物保留 7 天后自动过期；公开 Release 和已验证回退版本保留，不自动删除未知版本或用户配置。首次发布无过期公开版本需要清理。版本回退优先使用已验证的上一安装包，先保存用户配置，保持原生菜单恢复入口可用；配置数据不随撤回 Release 删除。
