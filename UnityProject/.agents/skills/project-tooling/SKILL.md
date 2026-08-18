---
name: project-tooling
description: 读取并维护项目的 Plugin、Architecture、System 工程能力目录。用于功能设计/实现涉及序列化、动画、资源、启动、依赖注入、异步、编辑器扩展或其他工程能力时；也用于用户要求检测、查看或更新技术栈与可复用架构时。
---

# Project Tooling

权威目录是 [tooling-catalog.json](references/tooling-catalog.json)，字段契约见 [CATALOG_SCHEMA.md](references/CATALOG_SCHEMA.md)。目录进入 Git，供 Agent 与 Workbench 共同读取；`.agent-memory/zworkflow/local/tooling-discovery.json` 只是可删除的来源指纹缓存。

## 实现任务路由

1. 任务涉及目录中任一 `capabilities`、程序集、证据路径或依赖时，只读取命中的条目，不默认加载整个技术栈文档。
2. `plugin`：`decisionBasis` 非空时作为项目级实现依据；为空时核对现有调用、程序集边界、资产/存档兼容性后自行判断，并在交付中简述证据。插件存在不等于必须使用。
3. `architecture`：必须为 `usagePolicy=required` 且 `locked=true`。适用时必须复用，不得建立平行替代；修改其条目、公共契约或对应公共实现前，必须取得用户对本次修改的明确确认。
4. `system`：表示与当前项目领域、GameManager、具体阶段或内容契约耦合的系统；按目录依赖使用 Plugin/Architecture。
5. 目录与代码冲突时先核验代码；事实字段可以据实修正，但 Architecture 仍受确认门禁。删除/降级已缺失 Plugin 前检查团队分支和平台条件。

## 分类边界

- `plugin`：外部包、SDK 或 Asset 插件，例如 Odin、DOTween、Animancer。
- `architecture`：删除当前游戏领域类型后仍可独立编译、测试和复用的完整框架能力。
- `system`：认识具体玩法、阶段、GameManager、项目数据或内容契约的实现。

按能力而非每个 Class 建条目。第三方 Plugin、通用适配层和具体项目集成应分别分类。

`kind` 与项目分层是两个维度：`kind` 表示归属/复用策略，可选 `layerIds` 表示实现落点。不要把 Data、Adapter、View 变成 System 子类型，也不要因为一个功能横跨三层就复制三个能力条目；项目没有明确分层时可完全省略层定义。

生命周期、启动编排、状态机或管线不能只因存在于项目入口目录就归为 `system`。先剥离具体玩法、UI 文案、内容包名称和业务入口：若剩余的生命周期阶段、扩展点与调度契约可跨项目独立复用，则该能力属于 `architecture`；仅把当前项目接到这些扩展点的组合代码属于 `system`。同一证据同时包含两层时拆成 Architecture 与项目集成 System，不能用具体接入代码掩盖可复用架构边界。

## 增量发现

setup 或用户要求重新检测时，定向检查 `Packages/manifest.json`、`Packages/packages-lock.json`、项目内插件目录、DLL/asmdef、Scripting Define 和命名空间引用。把来源路径、大小与修改时间的摘要写入 Git 忽略的 `tooling-discovery.json`；指纹未变化时复用现有目录，不全仓扫描。

发现只创建或更新带证据的 Plugin/System 候选：不得自动填写用户 `decisionBasis`，不得未经确认创建或改写锁定 Architecture。版本无法确认时留空，不猜测。

## 模块边界拆分

setup 不得把整个引擎、框架或应用聚合成单个笼统条目。按以下证据识别稳定模块：

1. README/Wiki 的核心模块表或独立模块章节。
2. 独立目录、公共接口/入口类型、asmdef 或明确的调用入口。
3. 可单独说明的职责、约束、验证方式和依赖。

System 的使用策略应写明主要系统类或接口、何时调用，以及明确不由它负责的内容；用户不应只看到“某某 GameCore”这类无法路由到入口的总括描述。

同一框架中的 Resource、Event、Config、UI、Procedure、MemoryPool、ObjectPool 等满足上述边界时分别建条目；跨模块启动编排另建 `system`，不能替代模块条目。不要按每个 Class 拆分，也不要把仅有标题、没有代码或配置证据的历史模块误报为已实现。

文档存在但实现缺失时，可以在用户确认后保留 Architecture 候选，但必须在描述与 `constraints` 中明确 `partial` 事实和缺失输出；不得声称可用。Architecture 仍保持 `usagePolicy=required`、`locked=true`。

## Architecture 工具类使用策略

创建或把条目重分类为 Architecture 时，判断它是否属于工具类 Architecture：存在可由其他模块或业务代码直接调用的公共 API、服务入口、模块接口、静态工具或基础设施能力，并且调用方需要遵守选型、所有权、生命周期或释放约束。纯粹描述分层、边界、阶段顺序或概念模型且没有直接消费入口的 Architecture 不算工具类。

对工具类 Architecture，在首次创建条目时必须根据代码、接口、调用点与文档证据自动生成 `usageNotes` 和语义对齐的 `usageNotesEn`：

1. 说明何时使用以及不适用的场景。
2. 指定业务层首选入口/API；区分组合根、框架内部与普通调用方时分别写清。
3. 写明对象所有权、初始化、取消、释放、归还或关闭规则；不适用的项省略。
4. 指出应避免的平行实现、误用或替代边界。
5. 只写证据可确认的规则，不把 `constraints` 原样拼接成策略。

“自动生成一次”是初始化语义：`usageNotes` / `usageNotesEn` 已有任一非空内容时，setup 或重新发现不得覆盖用户编辑；只有用户明确要求重写策略时才能替换。若新证据使现有策略可能过期，报告差异并等待用户决定。非工具类 Architecture 可以不生成使用策略，但仍必须满足 required/locked 门禁。

## 写入规则

- UTF-8 写入，稳定 ID 使用 kebab-case；数组缺失时写空数组。
- `decisionBasis` 只属于 Plugin。Workbench 可直接编辑该字段。
- `usagePolicy` 是 Agent 使用的机器门禁级别；用户在 Workbench 输入的自由文本保存到 `usageNotes` / `usageNotesEn`，不得用自由文本覆盖 Architecture 的 `required`。
- 工具类 Architecture 首次建档时必须自动生成中英文 `usageNotes`；后续发现不得覆盖已有内容。
- 面向工作台展示的项目生成目录同时写入 `displayNameEn`、`descriptionEn`、`capabilitiesEn`、`constraintsEn`；英文数组与对应中文数组语义对齐。稳定 ID、路径、版本和依赖不翻译。
- Architecture 的 `usagePolicy` 和 `locked` 不得被降级；用户确认后由 Agent 修改并记录代码/文档证据。
- 新持久化字段必须同时有 Agent 或 Workbench 消费者；不复制长篇第三方文档。
