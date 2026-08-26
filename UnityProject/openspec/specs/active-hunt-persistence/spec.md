---
schemaVersion: 2
category: architecture
title: "活动狩猎检查点与恢复"
---

# Active Hunt Persistence Specification

## Purpose

让普通狩猎在退出游戏后从最近一次已提交 Action 检查点继续，并保证活动狩猎与待结算回营记录不会同时成为权威状态。

## Requirements

### Requirement: Campaign snapshot declares its active phase explicitly

战役存档 SHALL 使用显式活动狩猎标志区分 Settlement 与 Hunt，不得依赖 `JsonUtility` 对可序列化引用的空值表现。读取旧版 Settlement JSON 时 SHALL 迁移为无活动狩猎的战役快照；默认空回营记录 SHALL 归一化为空。

#### Scenario: A settlement-only snapshot is serialized and loaded

- **WHEN** 存档只包含营地状态
- **THEN** 读档 SHALL 保持活动狩猎为空
- **AND** 默认构造的空 `HuntRecord` SHALL NOT 触发回营门禁

### Requirement: Active Hunt snapshot freezes authoritative runtime state

活动狩猎检查点 SHALL 保存稳定远征 ID、BoundRoute 的目的地 ID 与 ContentBundleId、年份、1 至 4 名猎人、选中猎人、小队坐标、完整地图地块状态、资源点、携带物、事件 occurrence store 与可恢复随机状态。地块、物品与事件引用 SHALL 使用显式稳定 ContentId；目的地和 Bundle 身份不得由外部字符串参数注入。

#### Scenario: The process exits after a committed Hunt action

- **WHEN** Hunt Runner 已完成一次地块、采集或事件 occurrence 提交并处于空闲状态
- **THEN** GameManager SHALL 冻结新的活动狩猎载荷
- **AND** 退出保存 SHALL 使用最近一次完整载荷，不序列化正在执行中的半成品 Action

### Requirement: Active Hunt restore requires the same content generation

活动狩猎 schema v2 SHALL 携带非空 ContentBundleId。恢复 SHALL 从当前发布 Bundle 按 DestinationId 与年份只读解析精确 RoutePlan，并以 Ordinal 比较存档 BundleId、当前 BundleId 与 RoutePlan BundleId；任一不一致 SHALL 在构建地图或修改携带物前 fail closed。缺少 Bundle 身份的 active schema v1 SHALL NOT 自动迁移；旧 Settlement-only 存档 SHALL 继续兼容。

#### Scenario: A destination ID is reused by changed content

- **WHEN** 存档的 DestinationId 仍存在，但当前 Bundle 指纹与存档不同
- **THEN** 普通恢复 SHALL 被拒绝并保留原存档
- **AND** 当前 Manager、阶段与目的地 RuntimeState SHALL 保持不变

#### Scenario: An old settlement-only save is loaded

- **WHEN** 旧存档不包含活动狩猎状态
- **THEN** 系统 SHALL 继续把它恢复为 Settlement-only Campaign
- **AND** 不得要求不存在的 ContentBundleId

### Requirement: Restore validates before mutating live collectibles

恢复 SHALL 在替换地图、携带物或会话前验证 schema、Bundle/Route 身份、年份、随机算法、猎人、地块目录、资源目录、事件目录、小队位置和 occurrence。地块与事件 SHALL 来自 BoundRoute/HuntManager；资源和携带物 SHALL 由同一 RoutePlan 所属 RegistryBundle 解析。携带物 SHALL 先构建临时投影，全部验证成功后一次性替换。

#### Scenario: A referenced content ID no longer exists

- **WHEN** 活动狩猎快照引用缺失或重复的地块、物品或事件 ContentId
- **THEN** 恢复 SHALL 失败并给出诊断
- **AND** SHALL NOT 部分清空或写入猎人的运行时携带物

### Requirement: Restore publishes one candidate graph atomically

GameManager SHALL 捕获完整 Destination RuntimeState，再执行只读路线解析，并由 SettlementPhaseManager 与 HuntPhaseManager 准备和发布候选 runtime generations；随后生成稳定 payload、提交路线，并通过 Hunt composition coordinator 重建 ActionSession 与表现。任一失败 SHALL 恢复相同对象身份的 RuntimeState 与旧运行图，不得按 DestinationId 重新猜测；阶段回滚失败时 SHALL 保留完整候选 Manager、Route 与 payload，而非混合新旧状态。

#### Scenario: Hunt session presentation fails after candidate publication

- **WHEN** 候选 Manager 与 Route 已发布，但 Session 或 3D 表现初始化失败
- **THEN** 成功阶段回滚 SHALL 恢复此前完整运行图和目的地对象身份
- **AND** 阶段无法回滚时 SHALL 保留可运行、可保存的完整候选图

### Requirement: Random continuation is deterministic

狩猎随机源 SHALL 导出并恢复带算法身份的非零状态。恢复后的下一次随机消费 SHALL 与未退出进程的同一检查点产生相同序列。

#### Scenario: The same checkpoint is restored into another Hunt manager

- **WHEN** 两个运行环境从相同随机算法与状态继续
- **THEN** 后续随机值序列 SHALL 一致

### Requirement: Return handoff is mutually exclusive and recoverable

活动狩猎与 `PendingHuntReturn` SHALL 为互斥权威状态。撤退准备成功后 SHALL 先保存只包含待回营记录的 Settlement 快照，再销毁 Hunt session；阶段切换失败时 SHALL 恢复活动狩猎检查点，只有其保存成功后才解除回营锁。

#### Scenario: Settlement transition fails after preparing retreat

- **WHEN** 待回营记录已经保存但阶段 FSM 拒绝切换
- **THEN** 系统 SHALL 尝试恢复并保存活动狩猎状态
- **AND** 无法安全撤销时 SHALL 保留原待回营记录供重试，不产生双重权威快照

### Requirement: Current encounter lifecycle remains out of scope

本能力 SHALL 覆盖普通 Hunt session 的进程重启恢复，但 SHALL NOT 宣称跨当前 BossFight/遭遇阶段恢复已销毁的 Hunt session。Hunt 遭遇接纳前 SHALL 原子写入不可作为普通 Hunt 恢复的交接标记；重启时 SHALL fail-closed 并保留原运行态。遭遇生命周期调整需要独立方案，不得借此推进 Showdown 玩法。

#### Scenario: Hunt hands off to the current BossFight phase

- **WHEN** 现有阶段编排销毁来源 Hunt session
- **THEN** 存档 SHALL 标记未支持恢复的遭遇交接并拒绝普通 Hunt 恢复
- **AND** 当前版本 SHALL NOT 自动从战斗返回或复活交接前 Hunt
