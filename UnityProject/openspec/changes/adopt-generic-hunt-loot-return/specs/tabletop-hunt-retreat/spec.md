---
schemaVersion: 2
category: feature
title: 3D 狩猎回营结算
---

## MODIFIED Requirements

### Requirement: Retreat location creates a visible cargo decision

从营地位置回营 SHALL 保留全部携带物。远离营地紧急撤退 SHALL 要求玩家从当前聚合携带物中选择一个稳定物品 ID，并从准备好的回营快照中准确省略该物品一个单位；没有携带物的小队 MAY 直接撤退。3D 选择卡 SHALL 显示物品名称、类别和数量，但不得修改实时猎人携带物。

#### Scenario: The squad retreats away from camp with mixed cargo

- **WHEN** 小队远离营地且携带资源和非资源物品
- **THEN** 每种聚合携带物 SHALL 由一张世界空间选择卡表示
- **AND** 玩家可选择任一当前物品放弃一个单位
- **AND** 权威准备 Action SHALL 重新验证该物品仍存在，再只过滤回营快照

#### Scenario: The squad returns at camp

- **WHEN** 回营布局在营地位置打开
- **THEN** 确认卡 SHALL 表明全部携带物会按类别入库
- **AND** 伪造的放弃选择 SHALL 被拒绝

### Requirement: Hunt runner prepares the completion snapshot

活动 Hunt ActionQueue SHALL 在任何 Campaign 转换或库存转移前准备年份、出发人数、损失人数和 v2 通用携带物快照，Reactor SHALL 能阻止该准备。Runner SHALL 重新读取小队位置与实时货物；缺失、伪造、过期或营地限定的放弃选择 SHALL 失败且不得改变实时货物或发布准备事实。

#### Scenario: A Hunt reactor prevents mixed-cargo retreat

- **WHEN** 已注册 Reactor 拒绝包含非资源物品的撤退 Action
- **THEN** 不发布准备事实，不转移或丢弃任何物品，玩家保持在 Hunt
