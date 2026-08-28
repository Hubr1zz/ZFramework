---
schemaVersion: 2
category: feature
title: 狩猎桌面消耗品
---

## ADDED Requirements

### Requirement: Hunt consumables use the frozen item generation

狩猎消耗品 SHALL 使用稳定 ContentId、显式效果类型和正数效果量，并由当前 Hunt 绑定内容世代解析为 canonical 对象。首期只支持 `RecoverBodyPart` 且 HuntNoise 必须为零；未知、旧世代、非法数量、非 Consumable 或未支持效果 SHALL 在状态改变前失败关闭。

#### Scenario: An event grants a field dressing

- **WHEN** 当前 Hunt 事件把 `weathered_field_dressing` 交给执行者
- **THEN** 其实体携带卡 SHALL 标记为可在本次狩猎使用
- **AND** 相同 ContentId 但不属于当前内容世代的对象 SHALL NOT 被命令消费

### Requirement: Consumable use is a world-space tabletop interaction

当前行动猎人的可用 Consumable 携带卡 SHALL 可点击并打开四张世界空间身体部位卡。普通资源、武器和防具卡 SHALL 保持只读；关闭面板、满生命部位、非法选择或尚未提交时 SHALL NOT 修改携带物或生命。

#### Scenario: The player chooses an injured body part

- **WHEN** 玩家点击包扎布实体卡并选择一张受伤部位卡
- **THEN** View SHALL 只提交 owner HunterId、ItemId 与 BodyPart
- **AND** View SHALL 在等待结果时禁用重复选择且不得直接修改权威状态

#### Scenario: The player reopens the item while submission is pending

- **WHEN** 一次部位命令仍在等待 ActionQueue 完成
- **THEN** 再次点击原携带物卡 SHALL NOT 重新启用部位卡或产生第二次提交

### Requirement: Hunt ActionQueue owns consumable state changes

使用 SHALL 作为当前 Hunt runner 的 root Action 串行执行，并在写入前重验 session、事件恢复、遭遇/采集/回营门禁、当前选中猎人、存活可用状态、canonical 物品、携带数量、效果和目标部位。成功 SHALL 恰好扣除一件 owner 的携带物、通过 `HunterRecoveryRules` 恢复普通生命并发布 Hunt-scoped 玩法事实。

#### Scenario: The selected hunter treats an arm wound

- **WHEN** 当前行动猎人携带两件包扎布且手臂存在普通伤势
- **THEN** 一件包扎布 SHALL 被扣除且手臂恢复配置量
- **AND** Settlement 仓库、其他猎人的携带物、永久损伤、症状与死亡状态 SHALL 保持不变

#### Scenario: A reactor prevents use

- **WHEN** BeforeExecution Reactor 阻止命令，或物品/部位在排队期间失效
- **THEN** 生命、全部携带物与 Outbox SHALL 保持不变

### Requirement: First release is self-use by the current actor

首期 SHALL 只允许当前选中猎人使用自己的携带消耗品治疗自己。外部猎人、非当前猎人、死亡或不可用猎人、其他猎人的物品，以及空或伪造 ID MUST 被拒绝；能力 SHALL NOT 隐式实现跨猎人治疗或物品转移。

#### Scenario: Another squad member is supplied as owner

- **WHEN** 请求把非当前行动猎人作为 owner
- **THEN** 命令 SHALL 失败且两名猎人的生命与携带物均保持不变

### Requirement: Existing active Hunt checkpoints persist the result

成功 root SHALL 触发现有活动狩猎 checkpoint，并继续使用既有身体状态与 Collectibles 快照字段，不新增存档 schema 或 consumable pending 状态。读档 SHALL 恢复已提交后的生命和剩余数量，且 SHALL NOT 重放使用。

#### Scenario: The game saves after field treatment

- **WHEN** 包扎布使用成功后捕获并恢复活动狩猎快照
- **THEN** 恢复的猎人 SHALL 保持治疗后的生命与剩余携带数量
- **AND** 读档本身 SHALL NOT 再次扣除物品或恢复生命
