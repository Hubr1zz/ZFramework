---
schemaVersion: 2
category: feature
title: 狩猎桌面资源采集
---

## MODIFIED Requirements

### Requirement: 世界空间采集桌展示完整混合素材池

资源点 SHALL 以稳定资源点 ID 标识，并 MAY 配置由多个素材及重复份数组成的牌池。存在世界空间锚点时，采集 View MUST 在资源点旁展示完整牌池的背面 3D 卡，而不是把资源点压缩成单一素材或打开屏幕空间弹窗。

#### Scenario: 玩家选择破损雕像

- **GIVEN** 破损雕像配置 5 张混合素材牌且允许翻开 2 张
- **WHEN** 玩家在小队所在的已揭示地块选择该资源点
- **THEN** 桌面在资源点旁显示 5 张背面素材卡
- **AND** 任意未翻开的素材卡均可被选择

### Requirement: ActionQueue 冻结逐素材结果并限制任意选择

Hunt Runner MUST 在 `BeginHarvestAction` Reactor 窗口结束后冻结牌池顺序、每张素材身份和逐素材命中结果。View SHALL 只提交所选卡索引，ActionQueue MUST 拒绝越界、重复或超过允许翻牌数的选择；达到允许翻牌数后 MUST 仅提交已选择且命中的素材。

#### Scenario: 玩家从五张牌中选择两张

- **WHEN** 玩家依次选择第 4 张和第 2 张未翻素材牌
- **THEN** ActionQueue 只揭示这两个索引的冻结结果
- **AND** 第二次揭示后资源点耗尽并一次性提交这两张牌中的命中素材
- **AND** 其余三张牌不进入猎人携带物

### Requirement: 逐素材覆盖不得泄漏到整个资源点

影响采集命中率的 Reactor MUST 按每个素材的稳定 ContentId 和关键词计算，不得因为同一资源点内存在一个匹配素材而修改其他不匹配素材。

#### Scenario: 草药和器官共用资源点

- **GIVEN** 已掌握发明只提高草药采集率
- **WHEN** 资源点牌池同时包含草药和器官
- **THEN** 草药牌使用提高后的命中率
- **AND** 器官牌继续使用基础命中率

### Requirement: 混合牌池配置与活动狩猎存档有界且兼容

内容 Bundle MUST 拒绝空身份、非资源引用、非正份数、超过 `HarvestDrawPlan.MaximumCardCount` 的牌池，以及允许翻牌数大于牌池数量的配置。活动狩猎快照 SHALL 保存资源点身份、显示名和完整有序素材 ID；旧快照只有 `ItemId` 时 SHALL 将其恢复为至少覆盖 `DrawCount` 的重复单素材池。

#### Scenario: 读取旧单素材活动狩猎存档

- **WHEN** 资源点快照只有稳定 `ItemId` 和 `DrawCount`，没有素材 ID 列表
- **THEN** 恢复器用该素材构造兼容牌池
- **AND** 恢复后的允许翻牌数与旧存档一致

#### Scenario: 导入非法混合牌池

- **WHEN** 允许翻牌数超过展开后的素材牌数量
- **THEN** 内容 Bundle 构建失败并报告资源点配置原因
