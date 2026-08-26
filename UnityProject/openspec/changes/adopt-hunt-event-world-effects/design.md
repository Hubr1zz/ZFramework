## Context

狩猎事件需要影响地图，但共享 `EventSystem` 不应依赖 HuntManager，事件 JSON 也不应获得任意坐标写权限。当前地块交互已经持有不可跨会话保存的 `HuntTileInteractionCommit`，资源点耗尽状态也已经包含在活动狩猎 schema v2 中。

## Decisions

### World effect 是单次 Action 能力

共享事件事务只认识可选的 `IPlayableEventWorldCommand`。Hunt runner 用当前已提交的地块交互构造 `HuntTileEventWorldCommand`；Settlement 和其他阶段不提供该能力。JSON 只声明效果类型，不携带坐标或资源点 ID。

### HuntManager 保持地图状态权威

Manager 在写入前验证 commit、坐标、地块对象和资源点集合，然后一次性把当前地块全部未耗尽资源点设为耗尽。全部写入后只发布一次坐标级状态变化通知，View 根据权威状态重建该地块棋子。

### 沿用既有效果批次语义

每个 world effect 仍产生有序效果结果。坐标作为解析目标，受影响数量作为结果值；合法的重复耗尽是成功 no-op。效果批次不新增跨效果回滚，结果确认失败也不回滚已经提交的玩法状态。

### 恢复继续使用 schema v2

资源点 `IsExhausted` 已由活动狩猎快照保存。恢复 pending occurrence 时，Runner 只从已验证地图坐标重建提交上下文，不从事件数据注入目标。

## Risks / Trade-offs

- 当前批次仍可能出现前一效果已提交、后一效果失败；结构化结果会暴露部分失败，本 Change 不引入通用 undo。
- 资源点棋子按坐标重建而非逐棋子打补丁，以较小场景规模换取状态一致性。
- 本轮只开放“耗尽当前地块资源”一种 world effect，任意坐标、生成地块和跨阶段世界修改留给独立设计。
