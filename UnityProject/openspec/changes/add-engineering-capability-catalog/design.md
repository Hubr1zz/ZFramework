## Context

当前项目事实分散在 project-context、代码、Packages 和插件目录中，Workbench 只展示 OpenSpec Feature/Architecture/Game Rule 图。旧 `architecture` 分类包含 GameManager 等项目耦合能力，与“可独立复用且功能完整”的真 Architecture 含义冲突。目录必须同时服务 Agent 定向读取和 Unity Workbench，且不能把可再生成的扫描缓存误当作团队权威事实。

## Goals / Non-Goals

**Goals:**

- 以单个 Git 同步 JSON 保存工程能力权威目录，并由一个共享 Skill 定义 Agent 消费、更新和确认门禁。
- 区分 Plugin、Architecture、System；按能力而不是逐 Class 建节点。
- Plugin 保存可编辑判断依据；空白时允许 Agent按项目风格判断，但必须说明证据。
- Architecture 始终为 required/locked，修改条目或公共实现前要求用户确认。
- Workbench 提供独立入口、关系图、详情和 Plugin 判断依据保存。
- 新数据写 `system`，旧 `architecture` 在读取时归一化为 `system`。

**Non-Goals:**

- 不自动安装、升级或删除第三方插件。
- 不把目录变成逐类型调用图、包管理器或许可证审计器。
- 不在每次 Agent 请求时全仓扫描，也不根据“存在插件”自动推断强制策略。
- 不在本次 apply 中直接改写正式 Specs；旧数据迁移由兼容读取和后续显式迁移完成。

## Decisions

### 单一 JSON 权威源

项目使用 `.agents/skills/project-tooling/references/tooling-catalog.json`。它是项目事实而非缓存，进入 Git，并同时由 Agent 与 Workbench 消费。可再生成的 discovery fingerprint 保存于 `.agent-memory/zworkflow/local/`，不进入 Git。

目录条目字段保持 Unity `JsonUtility` 可解析：稳定 ID、显示名、类型、说明、来源/版本、策略、判断依据、锁定状态、能力、约束、证据和依赖。Workbench 只允许直接修改 Plugin 的 `decisionBasis`，避免出现两个权威编辑入口。

### 三类语义

- Plugin：第三方依赖。`decisionBasis` 非空时 Agent 必须遵守；为空时依据现有代码风格、程序集边界和序列化兼容性判断。
- Architecture：不依赖当前游戏领域即可独立复用的完整能力。强制 `usagePolicy=required`、`locked=true`，Agent 修改目录或对应公共实现前必须确认。
- System：认识项目领域、GameManager、具体阶段或内容契约的项目系统。它替代旧 OpenSpec `architecture` 展示含义。

Plugin 本体、通用适配层和具体系统集成分别建节点。例如 Odin 是 Plugin，通用 Odin 配置框架可为 Architecture，角色配置编辑器为 System。

### 增量发现而非任务级全扫

setup 首次检查 package manifest/lock、插件目录、DLL/asmdef 和命名空间使用，生成有证据的候选并写入项目目录。后续比较来源指纹；只有变化时复核。发现只证明“存在”，不能自动生成非空判断依据或 Architecture 强制条目。

### Workbench 独立视图

顶部工具栏在关系图谱右侧增加“工程能力”。页面按 Plugin、Architecture、System 分列节点并绘制依赖边，左侧提供分类列表，右侧显示说明、策略、证据和约束。Plugin 显示可保存的判断依据；Architecture 显示锁定告警且不提供直接编辑。

### 分类兼容迁移

技能和新 metadata 使用 `system`。Workbench 的归一化层把 `architecture` 和 `system` 都转为 `system`；旧文件无需立即批量改写。任何执行分类门禁的 Skill 都以 `system` 为规范值并显式接受 legacy `architecture`。

## Risks / Trade-offs

- [插件被检测到但未实际使用] → 条目保留证据和确认状态，空判断依据只允许 Agent基于代码风格判断，不升级为强制策略。
- [Odin Inspector 与 Odin Serializer 被混用] → 判断依据同时记录展示、序列化和兼容性约束；GameCore/noEngineReferences 可明确禁止依赖。
- [Architecture 锁定导致维护困难] → 用户确认后仍可修改；门禁只防止 Agent 自主改动。
- [关系图节点过多] → 目录以能力/框架为粒度，不为普通 Class 建节点。
- [旧 Change 分类失效] → 读取时兼容 `architecture`，新写入统一为 `system`，验证规则同时接受旧值。
- [工作台模板与运行源码漂移] → 新 partial、现有 partial 和 setup templates 做逐文件哈希检查。

## Migration Plan

1. 安装通用 project-tooling Skill 和空 catalog 模板。
2. 为当前项目创建包含已核验 Odin、DOTween 与 GameManager System 的目录。
3. Workbench 加载新目录并显示独立页面。
4. 把 UI/Skill 的规范分类切换为 System，同时兼容旧 Architecture 数据。
5. setup、移植包、清单、文档与 ZIP 同步；旧正式 Specs 留待显式迁移。

回滚时可移除 Workbench 新 partial 与 project-tooling Skill；旧 `architecture` 数据未被破坏，旧版本仍可读取。

## Open Questions

无。Architecture 的新增与修改确认由具体任务中的用户授权承担，不在目录中保存永久豁免。
