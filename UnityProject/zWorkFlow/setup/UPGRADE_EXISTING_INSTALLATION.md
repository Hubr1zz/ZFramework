# Upgrade Existing zWorkFlow Installation

本流程只在用户显式要求 setup/升级，且存在一个比已安装版本更新的 zWorkFlow Pack 时执行。目标是替换工作流程序，不替换项目数据。

## 版本判断

1. 来源版本读取 `<source>/setup/PACKAGE_MANIFEST.json.packageVersion`。
2. 已安装版本读取 `<target>/setup/PACKAGE_MANIFEST.json.packageVersion`。
3. 按点分隔的多段数字版本比较，缺失尾段按 0 处理。来源版本小于或等于目标版本时停止替换并报告 `up-to-date`；不得用旧包降级。
4. 清单缺失、版本无法解析或 `upgradePolicy` 缺失时停止，不能猜测覆盖范围。

当来源目录名为 `zWorkFlow Pack`，且它的父目录本身含有 `setup/PACKAGE_MANIFEST.json` 时，父目录就是待升级的 zWorkFlow。否则目标为当前项目根下的 `zWorkFlow/`。

## 内容边界

`upgradePolicy.managedTopLevel` 是工作流程序边界，可以被新版本整体替换并删除旧版本遗留文件。

下列内容始终保留：

- `.agent-memory/`：成员映射、本机选择、个人偏好和运行状态。
- `openspec/`：正式 Spec、Change、翻译、设计来源和审计账本。
- `.agent-bridge/`、`.design-workflow/`：兼容读取所需的现有桥接状态；升级后是否迁移由对应工作流单独决定。
- 清单 `preserveTopLevel` 声明的其他目录或文件。
- 目标中不属于 `managedTopLevel` 的未知顶层内容；它们视为项目数据或本地扩展，不得因升级删除。
- 目标自身的 `.git/`、凭据、用户历史和项目根中的 `.agents/`、`.agent-memory/`、`openspec/`。

来源包内的空模板不得覆盖同名已保存数据。旧的 managed 文件若新版本不再提供，应随程序升级删除，避免已废止 skill 继续生效。

## 原子升级

1. 先建立目标数据清单与 SHA-256 基线。
2. 在目标同级创建临时目录，将来源包中除 `.git`、`.github`、`node_modules`、嵌套 Pack 和发布 ZIP 以外的内容复制进去。
3. 从旧目标向临时目录复制全部保留内容和未知顶层内容。
4. 验证来源清单 `requiredFiles`、JSON、入口链接和保留数据 hash。
5. 只对 `managedTopLevel` 中的顶层项逐项执行“旧项移入临时备份、新项移入目标”；保留项和未知项原地不动。任一项切换失败时按相反顺序恢复所有已切换项。
6. 切换成功后重新验证版本、requiredFiles、保留数据 hash 与包测试；全部通过后才能删除各项临时备份。
7. 运行普通 setup 的项目适配阶段，根据 `projectInstall` 和 `.agent-memory/zworkflow/install-state.json` 更新受新版本影响且可证明由 zWorkFlow 管理的已安装模板；更新后记录包版本、项目相对目标路径和 SHA-256。项目专属 skill、Spec、队列、个人规则和冲突文件继续保留。

CLI `zworkflow setup <项目目录>` 使用同一策略。Agent 手动执行时也必须遵守本文件，不能用无差别目录镜像代替版本化升级。

## 项目安装状态

`.agent-memory/zworkflow/install-state.json` 是 setup 的安装审计索引，不是项目事实。最小结构为：

```json
{
  "schemaVersion": 1,
  "packageVersion": "2026.08.04.2",
  "managedSkills": [{ "path": ".agents/skills/example", "sha256": "..." }],
  "unityWorkbench": {
    "root": "Assets/Scripts/Editor",
    "files": [{ "path": "AgentWorkbenchWindow.cs", "sha256": "..." }]
  }
}
```

所有路径必须相对项目根且使用 `/`。setup 更新前先用记录的 hash 判断目标是否仍由 zWorkFlow 管理；目标已被项目修改时不得覆盖。`.agents/skills/` 保持工具可发现的标准位置，不创建 `.agents/.zworkflow/skills` 平行目录。

## 报告

至少报告：

- `installed | upgraded | up-to-date | blocked`
- 来源版本与目标版本
- 被替换的 managed 顶层项
- 被保留的数据项及 hash 校验结果
- 已删除的旧 managed 文件
- 测试结果和回滚状态
