---
schemaVersion: 2
category: feature
title: 营地桌面资源卡布局
---

# Tabletop Settlement Resources Specification

## Purpose

让营地资源以可拖动的 3D 实体卡投影权威库存，并在制造、事件、回营等玩法提交触发刷新时保留玩家当前桌面摆放；布局只服务当前世界空间交互，不改变游戏性数据或持久化契约。

## Requirements

### Requirement: Positive settlement resources use stable 3D cards

资源区 SHALL 按稳定资源 ContentId 为每种正数库存投影一张 3D 资源卡，玩家可见文本 SHALL 使用活动内容目录中的显示名；零库存 SHALL NOT 留下资源卡。

#### Scenario: Settlement resources are first presented

- **WHEN** 资源区读取当前权威库存
- **THEN** 每种正数资源 SHALL 出现在兼容卡槽中并显示当前数量

### Requirement: Refresh preserves player-arranged cards

资源刷新 SHALL 复用仍存在的资源卡实例及其当前卡槽，只更新数量；耗尽时 SHALL 只移除目标卡，新出现或重新获得的资源 SHALL 使用首个兼容空槽，且 SHALL NOT 重排其他卡。

#### Scenario: One arranged resource changes while another is exhausted

- **WHEN** 玩家已调整资源卡位置，随后权威提交更新数量并耗尽另一资源
- **THEN** 保留资源的对象与卡槽 SHALL 不变，且仅耗尽卡的槽位被释放

#### Scenario: A resource is obtained after presentation

- **WHEN** 权威库存出现此前未投影的正数资源
- **THEN** 新卡 SHALL 进入兼容空槽，且已有资源布局 SHALL 保持不变

### Requirement: Structural refresh waits for an active drag

资源卡拖拽期间到达的结构刷新 SHALL 缓存最新权威正数资源快照，并 SHALL 仅在拖拽结束后应用最后一份快照。

#### Scenario: Inventory changes while a resource card is being dragged

- **WHEN** 拖拽期间连续收到耗尽与新增资源刷新
- **THEN** 当前拖拽 SHALL 正常完成，结束后资源区 SHALL 一次投影最新权威快照

### Requirement: Layout remains presentation state

资源卡槽位 SHALL 只属于当前 View 会话，不写入战役存档，也不创建 GameAction；资源数量仍 SHALL 只由玩法 ActionQueue 的权威提交改变。

#### Scenario: A player rearranges resource cards

- **WHEN** 玩家只在资源网格内拖动卡牌
- **THEN** 游戏性资源数据与 ActionQueue SHALL 保持不变
