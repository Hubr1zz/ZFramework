---
schemaVersion: 2
category: architecture
title: "战役年度出猎闭环"
---

# Campaign Year Loop Specification

## Purpose

保证玩家只能从营地提交一次明确小队后进入狩猎，并在撤退回营时可靠提交记录、推进一年、清理检查点，再从新年份发起下一场远征。

## Requirements

### Requirement: Runtime Hunt transition requires a committed roster

Settlement 到 Hunt 的正式阶段转换 SHALL 要求一个由 Settlement Runner 提交的出发名册。名册 SHALL 包含 1 至 4 名唯一且当前可用的猎人；缺失、重复、失效或陈旧名册 SHALL 阻止转换。

#### Scenario: A caller bypasses the departure command

- **WHEN** 代码直接请求 Settlement 到 Hunt 转换但没有已提交名册
- **THEN** Campaign 组合根 SHALL 拒绝转换
- **AND** Hunt SHALL NOT 回退为全部可用猎人

### Requirement: Departure roster is a one-shot token

正式转换 SHALL 在 FSM 接受转换前冻结已验证名册，并在必需的 Hunt runtime、ActionSession 与交互入口成功创建后消费持久的 DepartingHunterIds。初始化失败 SHALL 回滚 FSM 并保留仍有效的名册以供玩家重试。

#### Scenario: The phase transition is rejected

- **WHEN** Settlement Runner 已提交名册，但 FSM 拒绝进入 Hunt
- **THEN** DepartingHunterIds SHALL 保持不变

### Requirement: Departure token cannot survive as stale authority

Settlement Runner SHALL 为名册提交当前年份、持久 preparation token 与仅当前运行时有效的对应 token。正式转换 SHALL 要求三者与当前年份一致；读档只恢复 ID 或持久 token 时 SHALL NOT 获得出发权限。

#### Scenario: A save contains an old prepared roster

- **WHEN** 存档恢复了 DepartingHunterIds 和持久 preparation token，但没有本次运行时 token
- **THEN** Settlement 到 Hunt 的正式转换 SHALL 被拒绝

### Requirement: Development Hunt boot is an explicit exception

仅 devStartPhase 直接启动 Hunt、且未经过 Settlement 到 Hunt 转换时，组合根 MAY 使用全部可用猎人作为开发回退。开发回退仍 SHALL 满足 1 至 4 名当前可用猎人的基础编队规则；该回退 SHALL NOT 放宽正式运行时转换门禁。

#### Scenario: The developer starts directly in Hunt

- **WHEN** devStartPhase 被明确配置为 Hunt 且没有已提交名册
- **THEN** Hunt MAY 使用当前 1 至 4 名可用猎人启动

#### Scenario: Development fallback roster is empty or oversized

- **WHEN** devStartPhase 被明确配置为 Hunt，但当前可用猎人为 0 名或超过 4 名
- **THEN** 开发回退 SHALL 拒绝创建不可玩的 Hunt runtime
- **AND** 组合根 SHALL 回到 Settlement

#### Scenario: Development Hunt runtime initialization fails

- **WHEN** devStartPhase 的 Hunt runtime 无法完成初始化
- **THEN** 组合根 SHALL 回退到 Settlement 并恢复可用的 Settlement Runner

### Requirement: View failure does not corrupt authoritative Hunt entry

HuntManager 与 Hunt ActionSession SHALL 构成正式入场的必需运行环境。3D 地图或辅助 UI 初始化失败 MAY 降级并记录诊断，但 SHALL NOT 清除已成功创建的权威 Hunt session；必需运行环境失败 SHALL 清理临时表现、回滚阶段并保留出发令牌。

#### Scenario: Auxiliary Hunt UI throws during initialization

- **WHEN** Hunt ActionSession 已创建，但回营面板或 Hunt UI 初始化异常
- **THEN** Campaign SHALL 保持可用的 Hunt ActionSession
- **AND** 失败的辅助表现 SHALL 被清理或降级

### Requirement: Accepted return advances exactly one year

Hunt Runner SHALL 生成稳定 HuntRecord；Settlement Runner 接受该记录后 SHALL 在单一 root 中提交历史、成长和 Timeline.AdvanceYear。一次被接受的回营 SHALL 只推进一年，重复记录 SHALL NOT 再次推进。

#### Scenario: The player retreats from the first hunt

- **WHEN** 年份 1 的撤退记录首次被 Settlement Runner 接受
- **THEN** CurrentYear SHALL 变为 2
- **AND** HuntHistory SHALL 只增加一条记录

### Requirement: Return checkpoint clears by stable record identity

回营结果可靠保存后，组合根 SHALL 仅在 PendingHuntReturn 与已提交 HuntRecord 的稳定 RecordId 一致时清理检查点和遗留出发名册。清理后的状态 SHALL 再次允许玩家在新年份提交出发名册；身份不一致 SHALL fail closed。

#### Scenario: A different record attempts to clear the checkpoint

- **WHEN** 已提交记录的 RecordId 与 PendingHuntReturn 不一致
- **THEN** PendingHuntReturn SHALL 保持不变
- **AND** 新的出猎 SHALL 继续被阻止

### Requirement: Production runner composition proves the loop

数据验证 SHALL 组合生产使用的 Settlement departure、Hunt retreat、Settlement return runners 与 Campaign loop contract，覆盖出发、消费名册、撤退、年份推进、检查点清理和下一次出发。该 smoke SHALL NOT 依赖 Showdown 流程。

#### Scenario: A complete non-Showdown loop runs

- **WHEN** 玩家从年份 1 出发并立即撤退回营
- **THEN** 年份 2 SHALL 可以再次提交同一可用猎人的新出发名册
