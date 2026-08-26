---
schemaVersion: 2
category: feature
title: 营地事件下一次狩猎风险租约
---

## ADDED Requirements

### Requirement: Settlement event options can create one persistent next-Hunt lease

Random、Scheduled 或 Triggered 类营地事件的选项效果 SHALL 可通过稳定 SourceEventId 创建一个 `PendingHuntNoiseLease`。该效果 SHALL NOT 用于 immediateEffects 或 Hunt 类事件。租约 SHALL 包含 schema、稳定 LeaseId、来源事件与正数修正；未知版本、无效来源或越界修正 SHALL 失败关闭。

#### Scenario: The risky vigil option is selected

- **WHEN** 玩家在 `random_stone_vigil` 选择承担风险的选项
- **THEN** 营地 SHALL 保存稳定租约 `hunt-noise:random_stone_vigil`
- **AND** 玩家 SHALL 获得该选项声明的两份 `black_salt`

#### Scenario: The effect appears in an invalid scope

- **WHEN** 内容把创建租约配置在 immediateEffects 或 Hunt 类事件
- **THEN** 整批事件内容 SHALL 失败关闭
- **AND** 不得发布部分可用的事件对象图

### Requirement: Event option batches preflight lease conflicts

事件选项事务 SHALL 在应用同批资源收益前预检租约。相同 LeaseId、来源和修正的重放 SHALL 幂等成功；已有不同 pending 租约、相同 ID 的冲突数据或非法租约 SHALL 拒绝整批选项，不得留下部分奖励。

#### Scenario: A replay reaches the same option

- **GIVEN** 同一租约已经由该事件提交
- **WHEN** occurrence 恢复重放相同选项
- **THEN** 租约 SHALL 保持单份且结果 SHALL 幂等
- **AND** installer registry SHALL NOT 增加重复 registration

#### Scenario: Another lease is already pending

- **WHEN** 新选项尝试覆盖不同 pending 租约
- **THEN** 选择事务 SHALL 失败
- **AND** 租约、资源和事件进度 SHALL 保持提交前状态

### Requirement: Campaign lifecycle projects the lease into Hunt environments

Campaign-owned 持久效果投影 SHALL 通过现有 `IActionEnvironmentInstallerRegistry` 注册 immutable、Hunt-only installer。当前及未来 Hunt ActionEnvironment SHALL 获得对应 Reactor；Settlement、Campaign 和 Combat 环境 SHALL NOT 安装它。阶段 session、View 与 Hunt runtime SHALL NOT 读取具体租约。

#### Scenario: A Hunt session is recreated before return

- **GIVEN** 租约仍在营地权威数据中
- **WHEN** 当前 Hunt session 释放并创建新的 Hunt environment
- **THEN** 旧环境的 Reactor registration SHALL 被释放
- **AND** 新环境 SHALL 自动安装同一租约 Reactor

#### Scenario: Campaign authority swap fails

- **WHEN** 候选营地世代或候选 installer 无法发布
- **THEN** 旧营地权威与旧 installer registration SHALL 保留
- **AND** 不得留下候选 registration

### Requirement: The lease modifies every noise check in the next Hunt

存在有效租约时，下一次 Hunt session 内每个 `ResolveHuntNoiseAction` 的计划值 SHALL 增加租约修正，再由既有风险牌 profile 的边界规则限制。Reactor 只处理玩法 Action，不处理 UI、动画或输入事件。

#### Scenario: The production lease is active

- **GIVEN** `random_stone_vigil` 的 `+2` 租约已投影到 Hunt environment
- **WHEN** Hunt 创建一次噪音检定计划
- **THEN** 计划噪音 SHALL 在原始值上增加 2
- **AND** 结果 SHALL 仍服从既有 profile 的最小与最大边界

### Requirement: The lease persists and restores fail closed

营地快照 SHALL 保存 pending 租约；旧档缺失或为 null SHALL 视为无租约。读档 SHALL 在发布 Settlement authority 时重建 installer，并使随后恢复或新建的 Hunt environment 自动获得 Reactor，而不修改 ActiveHunt schema。非法持久数据 SHALL 阻止候选发布。

#### Scenario: An active Hunt is restored

- **GIVEN** 存档包含有效租约与 active Hunt
- **WHEN** Campaign 恢复营地权威并重建 Hunt session
- **THEN** 恢复后的噪音检定 SHALL 获得相同修正
- **AND** 不需要向 ActiveHunt 数据复制租约

### Requirement: Only successful authoritative Hunt return consumes the lease

成功回营 Action SHALL 在资源、猎人成长和日历提交前清除 pending 租约及 Campaign installer registration。回营验证失败、Reactor prevent、取消、出发失败或只恢复读档 SHALL NOT 消费租约。已应用回营记录的恢复分支 SHALL 幂等完成清理。

#### Scenario: Return validation fails

- **WHEN** 回营记录无效或 Apply Action 被 Reactor 阻止
- **THEN** pending 租约与 installer registration SHALL 保留
- **AND** 回营资源、成长与日历 SHALL 保持不变

#### Scenario: Return commits successfully

- **WHEN** 权威回营 Action 成功提交
- **THEN** 租约与对应 installer SHALL 在其他回营 mutation 前清除
- **AND** 后续 Hunt environment SHALL NOT 再获得该 Reactor
