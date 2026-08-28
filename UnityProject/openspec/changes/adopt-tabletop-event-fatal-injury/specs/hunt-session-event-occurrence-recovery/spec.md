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
