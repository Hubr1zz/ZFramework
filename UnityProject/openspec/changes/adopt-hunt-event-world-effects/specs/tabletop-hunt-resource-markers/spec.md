---
schemaVersion: 2
category: feature
title: 3D 狩猎资源点棋子
---

## MODIFIED Requirements

### Requirement: Exhausted markers are removed safely

任一 Hunt ActionQueue 提交将资源点标记为耗尽后，地图 SHALL 立即停用旧棋子，再按 HuntManager 权威状态重建对应地块的剩余棋子。状态变化通知 SHALL 表达资源点状态已改变，不得伪装成玩家完成了一次采集。

#### Scenario: One of two points is exhausted by harvesting

- **WHEN** 采集提交将第一个资源点标记为耗尽
- **THEN** 其棋子立即停止接收输入
- **AND** 第二个资源点以原始命令索引重建

#### Scenario: An event exhausts every point on its tile

- **WHEN** 当前地块事件提交资源耗尽 world effect
- **THEN** 该地块所有资源棋子在状态通知后被移除
- **AND** View 不直接修改任何 `ResourcePointInstance`
