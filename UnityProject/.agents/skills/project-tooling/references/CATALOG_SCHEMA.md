# Engineering Capability Catalog Schema

`tooling-catalog.json` 是 Git 同步的项目事实与策略权威源。顶层包含 `schemaVersion`、`updatedAt`、可选的 `layers` 与 `entries`。每个条目包含稳定 `id`、`displayName`、`displayNameEn`、`kind`、`description`、`descriptionEn`、`version`、`source`、`usagePolicy`、可选的 `usageNotes`/`usageNotesEn`、`decisionBasis`、`locked`、`capabilities`、`capabilitiesEn`、`constraints`、`constraintsEn`、`evidence`、`dependencies` 和可选的 `layerIds`。

- `kind`：`plugin | architecture | system`。
- `kind` 回答能力的归属与复用边界；`layerIds` 回答实现位于哪些代码层。二者是正交维度，Data/Adapter/View 不得编码成 System 子类型。
- `layers` 由当前项目声明稳定 ID、中英文名称与说明；条目的 `layerIds` 可引用零到多个层。无明确分层的项目省略 `layers`/`layerIds`，采用其他分层结构的项目可声明自己的层，Workbench 不硬编码三层名称。
- Plugin 的 `decisionBasis` 可为空；非空时是 Agent 权威使用依据。
- Architecture 必须 `usagePolicy=required`、`locked=true`，修改前确认用户。
- Workbench 只直接修改 Plugin 的 `decisionBasis`。
- 读取端容忍未知字段；版本无法确认时留空。
- `displayName`、`description`、`capabilities`、`constraints` 保存中文；对应 `*En` 字段保存语义对齐的英文。稳定 ID、版本、来源、证据和依赖保持语言无关。
- `usagePolicy` 保存 `available` / `required` 等机器策略；Workbench 的可编辑“使用策略”正文按当前语言分别保存到 `usageNotes` 或 `usageNotesEn`，不得改变 Architecture 门禁。
- 工具类 Architecture（具有供其他模块或业务代码调用的公共 API、服务入口、模块接口、静态工具或基础设施能力）首次建档时必须包含非空且语义对齐的 `usageNotes` / `usageNotesEn`。两者说明适用与非适用场景、首选入口、生命周期/所有权规则和应避免的误用；已有任一非空策略时，setup 与增量发现不得自动覆盖。
- 仅描述边界、阶段顺序或概念模型且没有直接消费入口的 Architecture 不要求生成 `usageNotes`，也不得因此降级其 required/locked 门禁。
- 能力按稳定模块边界保存，不按整个框架聚合，也不按单个类碎片化。文档已确认但实现不完整时，把缺失事实写入 `description`/`constraints`；读取端不得据此推断已实现。
- System 条目应在 `usageNotes` 中列出主要系统入口、适用时机和不负责的边界；同一稳定功能可横跨多个 `layerIds`，无需为 Data、Adapter、View 各复制一份能力条目。
