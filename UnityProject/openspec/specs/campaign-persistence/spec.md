---
schemaVersion: 2
category: feature
title: 战役持久化与恢复
---

# Campaign Persistence Specification

## Purpose

保证营地长期状态在正常操作、立即退出与继续战役之间保持一致，并在加载后重建 3D 桌面所需的运行时引用。

## Requirements

### Requirement: Committed settlement state is persisted

营地 ActionQueue 提交的资源、发明、工坊、猎人和装备变化 SHALL 通过持久化 Adapter 异步保存；View SHALL NOT 直接写入存档。

#### Scenario: A settlement transaction commits

- **WHEN** Settlement runner 发布已提交事务
- **THEN** GameManager SHALL 请求保存同一份 SettlementInstance 权威状态

### Requirement: Application exit flushes the latest snapshot

应用退出时 SHALL 在生命周期取消令牌失效前同步刷新当前营地快照；退出快照 SHALL 使用与后台保存相同的版本门禁，使较旧任务不能覆盖较新状态。

#### Scenario: The player exits immediately after state changes

- **WHEN** 当前营地状态已经变化但后台保存尚未完成
- **THEN** 退出流程 SHALL 落盘最新快照，较旧后台写入 SHALL 被忽略

### Requirement: Continue restores playable runtime state

继续战役 SHALL 恢复年份、资源、发明、工坊、猎人和装备名称，并通过内容目录重建非序列化装备实例后再释放 3D 营地桌面。

#### Scenario: A saved hunter has equipment

- **WHEN** 玩家从 3D 开场卡继续已有战役
- **THEN** 猎人的运行时装备集合 SHALL 与持久化装备名称一致，营地桌面 SHALL 显示恢复后的权威状态
