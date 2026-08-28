---
schemaVersion: 2
category: feature
title: 狩猎会话事件链恢复
---

## ADDED Requirements

### Requirement: Fatal injury keeps the occurrence commit boundary

致命伤的洗牌与桌面等待 SHALL 位于 occurrence 提交之前。表现取消、无效选位、Reactor prevent 或准备失败 SHALL 保留 root occurrence，且 SHALL NOT 写回伤势、死亡牌或死亡后果；提交完成后的结果表现失败 SHALL NOT 重放死亡牌或永久死亡。

#### Scenario: Death-deck presentation is cancelled

- **WHEN** 玩家在选择前取消或所属 Presenter 失效
- **THEN** 猎人生命、死亡牌堆、名册、装备和后续玩法随机流 SHALL 保持原值
- **AND** 下一条 Hunt 命令 SHALL 先恢复该 occurrence

### Requirement: Survival follow-up has an independent frozen-actor occurrence

死亡牌父事件提交后，专属存活事件 SHALL 作为独立 child occurrence 保存稳定事件 ID、父链和父提交时的 actor ID。子事件失败或读档恢复 SHALL NOT 重放父事件、伤势或死亡牌。明确 actor ID 无法解析为仍存活的小队猎人时 SHALL fail closed 并保留 occurrence，且 SHALL NOT 改投其他猎人；只有旧 occurrence 没有 actor ID 时 MAY 使用兼容回退。

#### Scenario: Child fails after parent commit

- **WHEN** 父事件已经提交存活牌结果而 child 在完成前失败
- **THEN** 下一条 Hunt 命令 SHALL 只恢复 child
- **AND** 父事件与死亡牌 SHALL NOT 再次提交

#### Scenario: Frozen actor becomes invalid

- **WHEN** 待恢复 child 含明确 actor ID，但该猎人已死亡或不在活动小队
- **THEN** 恢复 SHALL 失败并保留 occurrence
- **AND** 其他猎人 SHALL NOT 接收该事件效果
