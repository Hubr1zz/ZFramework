---
schemaVersion: 2
category: architecture
title: "战役开场启动事务"
---

# Campaign Startup Transaction Specification

## Purpose

保证正式开场在玩家选择新战役或继续战役前不发布临时营地状态，并把存档验证、阶段运行图与 ActionQueue 环境作为一次可重试的启动事务提交。

## Requirements

### Requirement: Entry choice precedes authoritative campaign state

启用正式开场菜单时，GameManager SHALL 保持 `AwaitingChoice`，不得创建 Settlement、阶段 ActionSession、年度事件或新存档。无开场选择的开发与兼容入口 SHALL 保留原有自动启动行为。

#### Scenario: The opening card waits for player choice

- **WHEN** 正式 Bootstrap 已激活 GameManager，但玩家尚未选择入口
- **THEN** SettlementData SHALL 为空
- **AND** Campaign ActionSession SHALL NOT 运行
- **AND** 退出游戏 SHALL NOT 写入临时战役快照

### Requirement: Startup commands are typed and single-flight

新战役与继续战役 SHALL 通过返回 `CampaignStartupResult` 的异步命令执行。状态 SHALL 在 `AwaitingChoice`、`StartingNew`、`Loading` 与 `Active` 间显式变化；任一启动命令执行期间，重复入口命令与普通阶段转换 SHALL fail closed。

#### Scenario: Continue is double-clicked during storage I/O

- **WHEN** 第一个 Continue 仍在等待持久化端口返回
- **THEN** 第二个 Continue SHALL 被拒绝
- **AND** 持久化端口 SHALL 只收到一次 Load 请求

### Requirement: A dedicated transaction owns non-Unity startup orchestration

`CampaignStartupTransaction` SHALL own the startup lifecycle, persistence I/O, Settlement or Active Hunt candidate selection, and retry convergence. `GameManager` SHALL remain the Unity composition host for publishing phase roots, 3D presentation, and prepared runtime candidates, but SHALL NOT duplicate the new/continue transaction body.

#### Scenario: Startup behavior evolves

- **WHEN** new persistence validation or candidate preparation is added
- **THEN** the change SHALL be implemented in the startup transaction or its typed collaborators
- **AND** Unity lifecycle methods SHALL remain responsible only for binding and releasing the transaction host

### Requirement: Continue validates before publishing runtime

Continue SHALL 先通过现有 Settlement 或 Active Hunt 候选验证，再发布对应 Manager、Phase、ActionSession 与稳定载荷。Settlement 恢复 SHALL NOT 重复执行新战役年度投影；Active Hunt 恢复 SHALL 保留路线、内容 Bundle、地图、编队、随机状态与事件 occurrence。

#### Scenario: A settlement save is continued

- **WHEN** 一个有效营地存档通过候选验证
- **THEN** 其年份、猎人、资源与待恢复事件 SHALL 成为唯一权威运行态
- **AND** 新战役的 OnEnter 年度事件 SHALL NOT 额外生成

#### Scenario: An active hunt save is continued

- **WHEN** 活动狩猎快照与当前内容 Bundle 完全匹配
- **THEN** GameManager SHALL 直接发布 Hunt 阶段及其 Hunt ActionSession
- **AND** SHALL NOT 先发布一个临时新营地

### Requirement: Failed startup remains retryable

读档缺失、内容不匹配、候选验证失败、阶段初始化异常或生命周期取消 SHALL 撤销临时 Session、FSM 与表现引用，恢复 `AwaitingChoice`，并保留原存档供重试。失败 SHALL NOT 将部分候选写回持久化端口。

#### Scenario: Active Hunt content no longer matches

- **WHEN** Continue 无法解析快照记录的路线或 Bundle
- **THEN** GameManager SHALL 保持无权威战役运行态
- **AND** 玩家 SHALL 可以再次选择 Continue 或新战役

### Requirement: New campaign publishes only after confirmed replacement

新战役命令 SHALL 先完成已确认的旧存档删除，再创建初始 Settlement、Campaign/Settlement ActionSession 与首轮年度事件。成功后状态 SHALL 为 `Active`；初始化失败 SHALL 清理部分运行图并允许重试。

#### Scenario: Confirmed save deletion fails

- **WHEN** 持久化端口无法可靠删除旧战役
- **THEN** 新战役运行态 SHALL NOT 启动
- **AND** 开场 SHALL 保持可重试的 `AwaitingChoice`

### Requirement: Verification uses public commands without screenshots

PlayMode 数据验证 SHALL 覆盖等待选择零写入、新战役单次发布、Settlement 精确恢复、Active Hunt 精确恢复与延迟 Load 重入门禁。验证 SHALL 使用 Unity CLI 与 GameManager 公共命令，不依赖截图或 Showdown 流程。

#### Scenario: Startup flow is verified in batch mode

- **WHEN** Unity CLI 执行开场与年度闭环 PlayMode 测试
- **THEN** 所有入口状态、候选数据与 Session 断言 SHALL 通过
- **AND** 测试 SHALL NOT 启动 Showdown 或截图验证
