---
schemaVersion: 2
category: architecture
title: "配置化战役季节出猎闭环"
---

# Campaign Year Loop Specification

## Purpose

保证玩家只能从营地提交一次明确小队后进入狩猎，并在撤退回营时可靠提交记录、推进一个配置季节、清理检查点，再从当前或下一年份发起下一场远征。

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

仅显式调用开发启动配置、直接启动 Hunt、且未经过 Settlement 到 Hunt 转换时，组合根 MAY 使用全部可用猎人作为开发回退。正式 Bootstrap SHALL 使用生产运行配置并从 Settlement 启动，不得从内容配置隐式获得开发直启能力。开发回退仍 SHALL 满足 1 至 4 名当前可用猎人的基础编队规则；该回退 SHALL NOT 放宽正式运行时转换门禁。

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

#### Scenario: Production content requests another initial phase

- **WHEN** 正式 Bootstrap 安装战役内容
- **THEN** GameManager SHALL 使用生产运行配置并从 Settlement 启动
- **AND** 内容候选的 InitialPhase SHALL NOT 隐式开启开发模式

### Requirement: View failure does not corrupt authoritative Hunt entry

HuntManager 与 Hunt ActionSession SHALL 构成正式入场的必需运行环境。3D 地图或辅助 UI 初始化失败 MAY 降级并记录诊断，但 SHALL NOT 清除已成功创建的权威 Hunt session；必需运行环境失败 SHALL 清理临时表现、回滚阶段并保留出发令牌。

#### Scenario: Auxiliary Hunt UI throws during initialization

- **WHEN** Hunt ActionSession 已创建，但回营面板或 Hunt UI 初始化异常
- **THEN** Campaign SHALL 保持可用的 Hunt ActionSession
- **AND** 失败的辅助表现 SHALL 被清理或降级

### Requirement: Campaign calendar is frozen table-driven content

战役日历 SHALL 由稳定 CalendarId 和有序 SeasonDefinition 列表定义；每个季节首期只包含稳定 SeasonId 与显示名，不得隐式携带未声明效果。新战役 SHALL 冻结默认 CalendarId，存档 SHALL 保存 CalendarId、CurrentYear 与 CurrentSeasonIndex。已发布日历的季节数量或顺序发生变化时 SHALL 使用新的 CalendarId，并保留旧定义供旧档解析；GameManager 和流程代码 SHALL NOT 写死季节数量。

#### Scenario: A future calendar contains three seasons

- **WHEN** 内容目录把另一个稳定 CalendarId 配置为三个有序季节
- **THEN** 同一回营流程 SHALL 在第三次成功提交后才进入下一年
- **AND** 不得修改流程代码或增加季节分支

#### Scenario: A saved campaign uses a non-default supported calendar

- **WHEN** 读档的 CalendarId 仍存在于 supported calendar 目录，但已不是当前 default
- **THEN** 战役 SHALL 恢复原 CalendarId 和季节索引
- **AND** SHALL NOT 静默切换到新默认日历

### Requirement: Accepted return advances exactly one configured season

Hunt Runner SHALL 生成稳定 HuntRecord；Settlement Runner 接受该记录后 SHALL 在单一 root 中提交历史、成长和日历游标。一次被接受的回营 SHALL 只推进有序列表中的一个季节；越过最后季节时 SHALL 把季节重置为首项并只推进一年。重复记录 SHALL NOT 再次推进。

#### Scenario: The player retreats from the first hunt

- **WHEN** 年份 1 的撤退记录首次被 Settlement Runner 接受
- **THEN** 默认两季日历的 CurrentYear SHALL 保持 1
- **AND** CurrentSeasonIndex SHALL 从 0 变为 1
- **AND** HuntHistory SHALL 只增加一条记录

#### Scenario: The player completes the second season

- **WHEN** 年份 1、季节索引 1 的撤退记录首次被 Settlement Runner 接受
- **THEN** CurrentYear SHALL 变为 2
- **AND** CurrentSeasonIndex SHALL 重置为 0

### Requirement: Annual events occur only on a real year boundary

每次首次成功回营 SHALL 发布一份季节推进玩法事实和 HuntCompleted 事实，并从当前年份可用池创建至多一个绑定该 RecordId 的 Random Timeline occurrence。只有日历计划实际进入新一年时，Settlement Runner 才 SHALL 创建该新年份的 MainStory 与到期 Scheduled 年度 occurrence 并发布 YearAdvanced 事实；同年季节推进、出发失败、取消和读档恢复 SHALL NOT 创建年度 occurrence。

#### Scenario: The first season completes in the default calendar

- **WHEN** 年份 1、季节索引 0 的回营首次成功提交
- **THEN** HuntCompleted 与 SeasonAdvanced SHALL 各发布一次
- **AND** YearAdvanced 与年份 2 的年度 occurrence SHALL NOT 产生
- **AND** 当前年份可用 Random occurrence SHALL 与该 RecordId 精确绑定

#### Scenario: A return record is retried after persistence recovery

- **WHEN** 同一个稳定 RecordId 已经推进过季节但检查点仍待清理
- **THEN** 重试 SHALL NOT 再推进季节或年份
- **AND** SHALL NOT 重复创建年度或回营 Random occurrence

### Requirement: Legacy pacing fields migrate conservatively

`HuntsCompletedThisYear` 与 `HuntsPerYear` SHALL 只作为旧存档迁移字段。schema 1 存档 SHALL 保留年份并从冻结日历的首季继续；schema 0 只有在旧配额等于所选日历季节数且 completed 合法时才 MAY 映射到季节索引。配额不匹配 SHALL 保守落到首季并记录诊断，不得猜测或推进年份。未知 CalendarId、未来 schema、非法年份或越界季节 SHALL fail closed，且读档迁移 SHALL NOT 创建 Timeline occurrence。

#### Scenario: A schema-one save is loaded

- **WHEN** 旧存档已经采用一次回营跨年的 schema 1
- **THEN** CurrentYear SHALL 保持不变并绑定默认 CalendarId
- **AND** CurrentSeasonIndex SHALL 迁移为 0

#### Scenario: A future calendar schema is loaded

- **WHEN** 存档的 pacing schema 高于当前支持版本
- **THEN** 候选营地世代 SHALL 被拒绝
- **AND** 原数据 SHALL NOT 被降级或改写

### Requirement: Return checkpoint clears by stable record identity

回营结果可靠保存后，组合根 SHALL 仅在 PendingHuntReturn 与已提交 HuntRecord 的稳定 RecordId 一致时清理检查点和遗留出发名册。清理后的状态 SHALL 再次允许玩家在当前或下一年份提交出发名册；身份不一致 SHALL fail closed。

#### Scenario: A different record attempts to clear the checkpoint

- **WHEN** 已提交记录的 RecordId 与 PendingHuntReturn 不一致
- **THEN** PendingHuntReturn SHALL 保持不变
- **AND** 新的出猎 SHALL 继续被阻止

### Requirement: Campaign persistence is a replaceable composition port

GameManager SHALL 通过战役持久化端口执行存档存在性查询、保存、读档与删除 I/O，默认 Adapter SHALL 委托现有 SaveLoadSystem。开场菜单、开始菜单与开发面板 SHALL 只调用 GameManager 命令，不得直接访问另一存储。端口仅可在 GameManager 首次 Awake 前替换，且实现 SHALL 串行化异步与即时变更或保证最后一次调用获胜；载荷冻结与结构验证 SHALL 继续由既有纯逻辑执行。

#### Scenario: The return checkpoint cannot be persisted

- **WHEN** Hunt Runner 已生成回营记录，但持久化端口拒绝保存 PendingHuntReturn 检查点
- **THEN** 玩家 SHALL 留在 Hunt
- **AND** PendingHuntReturn 与 HuntHistory SHALL 保持未提交
- **AND** CurrentYear SHALL NOT 推进

#### Scenario: Applied return persistence is still running

- **WHEN** 回营状态已经应用，但其可靠保存或年度事件恢复仍未完成
- **THEN** Settlement 到 Hunt 的下一次出发 SHALL 被拒绝
- **AND** 保存与恢复完成后才 SHALL 重新开放出发

### Requirement: Production runner composition proves the loop

数据验证 SHALL 同时覆盖生产使用的独立 runners 与真实 GameManager 公共命令。PlayMode smoke SHALL 在空测试场景动态装配生产配置和内存持久化端口，通过 `DepartForHuntAsync`、`RequestRetreatAsync` 与公开读模型覆盖出发、消费名册、两次默认季节撤退、检查点清理和下一次出发。该 smoke SHALL NOT 依赖 Showdown 流程、场景资产或截图。

#### Scenario: A complete non-Showdown loop runs

- **WHEN** 玩家从年份 1、季节索引 0 出发并立即撤退回营
- **THEN** 年份 1、季节索引 1 SHALL 可以再次提交同一可用猎人的新出发名册

#### Scenario: Public GameManager commands run the loop

- **WHEN** PlayMode smoke 通过正式配置激活 GameManager，并完成两次公开出发与撤退命令
- **THEN** 第一次回营后 SHALL 保持年份 1 并进入季节索引 1
- **AND** 第二次回营后 SHALL 进入年份 2、季节索引 0
- **AND** PendingHuntReturn 与 DepartingHunterIds SHALL 清空
- **AND** 每次可靠清理检查点后 SHALL 可再次通过公开出发命令进入 Hunt
