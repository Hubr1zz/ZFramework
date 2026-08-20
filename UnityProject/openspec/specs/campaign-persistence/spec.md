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

继续战役 SHALL 恢复年份、资源、发明、工坊、猎人和稳定物品 ContentId，并通过内容目录重建非序列化装备实例后再释放 3D 营地桌面；旧版显示名键 SHALL 在内容注册后幂等迁移，未知内容 SHALL 保留而不是静默丢弃。

#### Scenario: A saved hunter has equipment

- **WHEN** 玩家从 3D 开场卡继续已有战役
- **THEN** 猎人的运行时装备集合 SHALL 与持久化装备 ContentId 一致，营地桌面 SHALL 显示恢复后的权威状态

#### Scenario: A legacy save uses display names

- **WHEN** 已注册物品的旧存档以显示名保存资源、仓库或猎人装备
- **THEN** 加载流程 SHALL 合并并转换为稳定 ContentId、只提升受支持的身份版本，重复执行迁移 SHALL NOT 重复库存或装备实例

#### Scenario: A save contains unknown or future content

- **WHEN** 存档包含当前目录无法解析的物品标识，或身份版本高于当前运行时
- **THEN** 未知标识 SHALL 保留，未来版本状态 SHALL NOT 被当前运行时降级或重写
