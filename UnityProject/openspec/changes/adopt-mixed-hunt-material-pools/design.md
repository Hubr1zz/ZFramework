## Context

设计文档把资源点定义为若干不同素材卡组成的牌池，允许翻牌数小于牌池总数。旧实现只有 `ResourcePointInstance.Resource`，既无法表达混合牌池，也会把针对某一素材关键词的 Reactor 套用到整个资源点。

## Decisions

### 资源点拥有牌池，素材保持既有 ItemData 身份

配置层以素材引用与份数组成牌池；运行时展开为有重复项的 `List<ItemData>`。`ResourcePointId` 识别资源点，`ItemData.ContentId` 识别每张素材，避免把资源点身份与素材身份混用。旧 `resource` 字段只作为空牌池时的兼容来源。

### ActionQueue 冻结结果，View 只选择索引

`BeginHarvestAction` 在 Reactor 窗口结束后按逐素材命中率洗牌并冻结不可变结果。View 只提交所选卡索引；`AdvanceHarvestAction` 校验该索引未翻开，并在达到 `RevealLimit` 后提交命中素材。UI 翻面、悬停和关闭仍是表现事件，不进入 ActionQueue。

### 逐素材应用覆盖效果

发明 Reactor 逐张检查素材关键词并写入以 ContentId 为键的命中率修正，避免混合牌池中一张草药让器官或石材同时受益。

### 存档保持向后兼容

新快照保存资源点 ID、显示名和有序素材 ID 列表。旧快照没有素材列表时，用 `ItemId` 重复到 `DrawCount`，因此不提升 schema 版本；内容 Bundle ID 仍负责阻止跨内容世代恢复。

## Risks / Trade-offs

- 牌池上限为 32 张，过大的内容配置在 Bundle 构建时拒绝。
- 采集事务不跨会话保存；活动事务期间的检查点仍由 Runner 空闲门禁控制。
- 当前只提供基础混合素材内容，空白牌、黑盐资产化和更多牌池表格留给后续内容阶段。
