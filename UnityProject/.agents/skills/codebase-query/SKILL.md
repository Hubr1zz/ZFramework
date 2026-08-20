---
name: codebase-query
description: 使用项目本地的可再生成 C# 结构索引，减少理解代码时的重复 rg/Read 调用。用于查询类、方法、候选调用者、文件影响范围和项目结构概览；结果只用于定位，负面结论和 Unity 隐式引用仍须读取源码或资源核验。
---

# Codebase Query

先用一个本地命令缩小候选范围，再读取少量源码确认。该 skill 借鉴
`codebase-memory-mcp` 的“预索引 → 关系查询 → 源码核验”方法，但不复制其
MCP、SQLite、Tree-sitter、向量搜索、ADR、UI、安装器或 watcher。

运行条件是 Windows 或 macOS 上的 PowerShell 7+（`pwsh`）。索引中的项目相对路径统一
使用 `/`，访问磁盘时再转换为当前系统分隔符；源码与 JSON 均显式按 UTF-8 处理。

## 入口

在项目根目录运行：

```powershell
& .agents/skills/codebase-query/scripts/run.ps1 <command> [参数]
```

常用命令：

```powershell
# 一次返回目录、命名空间、类型与方法概览
& .agents/skills/codebase-query/scripts/run.ps1 architecture

# 搜索文件、类型或方法
& .agents/skills/codebase-query/scripts/run.ps1 search -Query PlayerController

# 优先返回类型绑定后的调用者，并保留同名词法候选
& .agents/skills/codebase-query/scripts/run.ps1 callers -Query Publish
& .agents/skills/codebase-query/scripts/run.ps1 callers -Query GameLoop.Tick

# 返回修改某个文件时可能受影响的候选文件
& .agents/skills/codebase-query/scripts/run.ps1 impact -Path Assets/Game/Scripts/GameManager.cs

# 汇总当前 Git 改动及候选影响范围
& .agents/skills/codebase-query/scripts/run.ps1 changed
```

默认输出紧凑 JSON，适合 Agent 在一次工具调用中读取。索引缺失时会自动构建；代码
变化时只重新提取变化文件，再对紧凑结构事实重新绑定。构建进度写入 stderr 和 Git 忽略的本地进度快照，
不污染 stdout 的 JSON。确定性的正式索引位于 Git 管理的
`.agents/codebase-query/code-query-index.json`，其中只保存项目相对路径与源码内容哈希，不保存本机路径、文件时间或生成时间。本地 sidecar 仅用于复用已计算哈希，不进入 Git。

## 优先路由

涉及 C# 项目结构、类型/方法定位、候选调用者、文件影响范围或当前 C# 改动时，若本
skill 已安装且 `pwsh` 可执行，Agent 必须先调用最匹配的 `architecture`、`search`、
`callers`、`impact` 或 `changed` 命令，再读取少量命中源码核验。只有 skill 未安装、
PowerShell 7 不可用、命令执行失败或查询超出契约能力时才能回退 `rg`/原生源码检索，
并在结果中简述回退原因。索引不会替代源码核验，原生读取仍是第二阶段而非禁用项。

## 使用边界

1. `architecture`、`search`、`callers`、`impact`、`changed` 用于发现候选范围。
2. 优先使用 `Type.Method` 查询；`resolvedCallers` 比单独的方法名候选更精确。
3. 修改代码前，读取所有关键候选文件核验真实语义。
4. 不依据索引单独声明“没有调用者”“没有影响”或“代码未使用”。
5. Unity 生命周期、Inspector、Scene、Prefab、ScriptableObject、UnityEvent、反射、
   Addressables 和动态 EventBus 关系不在该静态索引的完整能力范围内。
6. 本索引是可删除、可再生成并由 Git 同步的派生索引，不是项目事实、ADR 或 OpenSpec 的权威源。
7. JSON 中的 `engine=codebase-query-regex-binding-v6` 与 `schemaVersion` 可用于确认本次
   结果确实来自索引工具；没有这些字段时不得声称已使用索引。

`scripts/run.ps1` 是稳定公共入口。实现脚本与类型绑定库通过文件内 capability marker
发现，因此可在本 skill 目录内重命名或移动；不要重命名公共入口，外部命令需要一个
稳定地址才能启动能力。

详细命令与准确性说明见 [QUERY-CONTRACT.md](references/QUERY-CONTRACT.md)。
