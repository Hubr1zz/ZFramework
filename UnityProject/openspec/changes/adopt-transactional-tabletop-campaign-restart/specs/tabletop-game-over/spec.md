---
schemaVersion: 2
category: feature
title: "3D 桌面战役终局"
---

## MODIFIED Requirements

### Requirement: Restart keeps the defeat tabletop authoritative until reset succeeds

重新开始选择 SHALL 向当前 Campaign ActionQueue 提交 typed gameplay 命令。终局实体卡及其背景输入门禁 SHALL 保持至命令成功；失败 SHALL 显示原因并允许重试，只有成功后才关闭表现、恢复 collider 并显示新 Settlement。

#### Scenario: Reliable deletion is still pending

- **WHEN** 玩家选择重写战役且持久化 Adapter 仍在删除旧恢复候选
- **THEN** 终局实体卡 SHALL 保持可见并阻断后台输入
- **AND** 当前权威 Settlement/Hunt generation SHALL NOT 被替换

#### Scenario: Restart fails

- **WHEN** 删除、候选保存或运行态发布失败
- **THEN** 终局实体卡 SHALL 显示失败原因并保持可重试
- **AND** 当前权威运行态 SHALL 保持不变

#### Scenario: Restart succeeds

- **WHEN** 新 Settlement generation、稳定快照与阶段运行态均发布成功
- **THEN** 终局实体卡 SHALL 关闭并恢复被阻断的 collider
- **AND** 玩家 SHALL 看到新战役 Settlement

### Requirement: Restart is a Campaign gameplay action

玩家重启 SHALL 由 Campaign ActionSession 串行执行并发布 committed fact；Before Reactor MAY 阻止或注入 gameplay 前置流程。View SHALL NOT 直接删除存档、替换 Manager 或切换阶段。

#### Scenario: A restart Reactor prevents execution

- **WHEN** Campaign Before Reactor 阻止 `RestartCampaignAction`
- **THEN** 持久化端口和运行态宿主 SHALL NOT 被调用
- **AND** typed 结果 SHALL 返回阻止原因
