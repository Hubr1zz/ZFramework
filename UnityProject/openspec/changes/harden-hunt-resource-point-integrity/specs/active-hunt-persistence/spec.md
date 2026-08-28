---
schemaVersion: 2
category: system
title: "活动狩猎检查点与恢复"
---

## MODIFIED Requirements

### Requirement: Restore validates before mutating live collectibles

恢复 SHALL 在替换地图、携带物或会话前验证 schema、Bundle/Route 身份、年份、随机算法、猎人、地块目录、资源目录、事件目录、小队位置和 occurrence。地块与事件 SHALL 来自 BoundRoute/HuntManager；资源和携带物 SHALL 由同一 RoutePlan 所属 RegistryBundle 解析。携带物 SHALL 先构建临时投影，全部验证成功后一次性替换。

每个地块的资源点 SHALL 来自该 BoundRoute 冻结地块配置，并满足地块总量、同类型 `maxPerTile`、配置 `DrawCount` 与素材池稳定 ID 多重集合。schema v4 SHALL 提供完整素材池。schema v2/v3 缺少完整素材池时，系统 SHALL 仅在单个 `ItemId` 可解析为资源且属于已知或唯一匹配的资源点配置时恢复旧单素材牌池；否则 SHALL fail closed。运行态展示名 SHALL 使用当前冻结配置，不信任存档展示缓存。

#### Scenario: A referenced content ID no longer exists

- **WHEN** 活动狩猎快照引用缺失或重复的地块、物品或事件 ContentId
- **THEN** 恢复 SHALL 失败并给出诊断
- **AND** SHALL NOT 部分清空或写入猎人的运行时携带物

#### Scenario: A saved resource point exceeds its frozen tile rules

- **WHEN** 活动狩猎快照包含未知资源点、超过地块或同类上限、错误翻牌数或不一致素材池
- **THEN** 整个恢复 SHALL 失败并给出资源点诊断
- **AND** 不得发布部分地图、随机状态或携带物

#### Scenario: A legacy active-hunt snapshot stores one material ID

- **WHEN** schema v2 或 v3 资源点缺少 `MaterialItemIds`，但 `ItemId` 可在当前 RoutePlan 中唯一归属于该地块资源点配置
- **THEN** 恢复 SHALL 按旧单素材语义构建 `DrawCount` 张牌
- **AND** 资源点稳定 ID 与展示名 SHALL 迁移为当前冻结配置

#### Scenario: A current snapshot omits its full material pool

- **WHEN** schema v4 资源点缺少完整 `MaterialItemIds` 或其多重集合与冻结配置不同
- **THEN** 恢复 SHALL fail closed
