---
schemaVersion: 2
category: feature
title: 营地桌面猎人卡交互
---

## ADDED Requirements

### Requirement: Retirement and replacement project as roster deltas

权威退休提交后，营地猎人区 SHALL 增量移除退休猎人的 3D 卡牌而不重排其余卡牌；经 Settlement 招募 Action 创建的替补猎人 SHALL 以新运行时 InstanceId 加入首个兼容空槽，并可通过正常编队命令参加下一次狩猎。

#### Scenario: A returning hunter retires

- **WHEN** 归来 root 提交该猎人的退休状态并发布名册变化
- **THEN** 该猎人卡 SHALL 从可用名册移除，其他猎人卡 SHALL 保持原槽位

#### Scenario: The player recruits a replacement

- **WHEN** 既有世界空间招募流程成功创建替补猎人
- **THEN** 新猎人卡 SHALL 出现在空槽，且其稳定 InstanceId SHALL 能被下一次出发命令选择
