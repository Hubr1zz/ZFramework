---
schemaVersion: 2
category: feature
title: 营地桌面成长训练与年鉴
---

## ADDED Requirements

### Requirement: Hunt event memories appear beneath their expedition

3D 营地年鉴 SHALL 将每次 HuntHistory 的事件记忆作为该远征摘要的只读子条目，并按持久化 occurrence 顺序保持邻接。同一年多次远征 SHALL 各自成组，不得因标题排序把事件移动到其他远征或时间线条目之间。

#### Scenario: Two hunts finish in the same year

- **WHEN** 同一年存在两次远征且每次均有多个事件记忆
- **THEN** 年鉴 SHALL 为每次远征先显示摘要，再显示其有序事件子条目
- **AND** 两组事件 SHALL NOT 相互穿插

### Requirement: Fatal injury history is player readable

年鉴 SHALL 对 FatalInjury 记忆显示死亡牌存活或死亡、受伤部位、剩余生命、永久损伤或猎人死亡。View SHALL NOT 显示 DeathDeckId、背面牌位等技术字段，也 SHALL NOT 通过 ActionQueue 调度只读展示。

#### Scenario: A hunter survives a fatal injury

- **WHEN** 远征记录包含抽到存活牌并获得永久损伤的 FatalInjury 效果
- **THEN** 年鉴 SHALL 显示存活牌、部位、剩余生命、永久损伤与猎人存活
- **AND** SHALL NOT 显示内部牌堆 ID 或选牌位置
