---
schemaVersion: 2
category: feature
title: 战役持久化与恢复
---

## ADDED Requirements

### Requirement: Continued campaigns preserve retirement without replaying presentation facts

保存并继续战役 SHALL 保留猎人的退休状态、已清空装备、归还后的仓库数量与唯一退休年鉴。恢复 SHALL 从权威快照重建 3D 名册和仓库，但 SHALL NOT 重新发布已经提交的 `HunterRetiredEvent` 或再次归还装备。

#### Scenario: The player continues after retirement was saved

- **WHEN** 有效快照包含退休猎人、归还装备和对应退休年鉴
- **THEN** 继续战役 SHALL 恢复相同权威状态且不显示新的退休归档卡
- **AND** 玩家 SHALL 仍能通过既有招募流程补员并正常出发
