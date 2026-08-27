---
schemaVersion: 1
category: gameplay
title: "配置化营地设施值守"
---

# Settlement Facility Duty

## Purpose

营地设施可以在配置化季节中派驻一名猎人，经过到期结算后以桌面骰产生人口结果；值守必须与出猎门禁、存档和世界空间营地交互保持一致。

## Requirements

### Requirement: Table-driven duty definition

正式营地内容 SHALL 从设施值守表加载稳定 ID、设施/发明前置、持续季数、骰子规格、人口结果区间和玩家文案。加载器遇到任意无效记录 SHALL fail closed；运行时 SHALL 使用冻结的内容定义。

#### Scenario: Invalid duty content is rejected

- **WHEN** 设施值守表包含重复 ID、非法季数或重叠人口结果区间
- **THEN** 内容加载 SHALL fail closed
- **AND** 当前已冻结的运行时定义 SHALL 保持不变

### Requirement: Assignment is an authoritative settlement action

派驻 SHALL 通过 Settlement ActionQueue 验证设施/发明、可用猎人和出猎资格，并保留至少一名可出猎猎人。成功状态 SHALL 保存 AssignmentId、CalendarId、绝对开始/到期坐标；阻止、取消或取消的动作 SHALL 不产生部分资源/人口写入。

#### Scenario: Assignment preserves a hunt-capable roster

- **WHEN** 玩家尝试派驻最后一名可出猎猎人
- **THEN** Settlement Action SHALL 拒绝该请求
- **AND** 值守、人口与猎人状态 SHALL 保持不变

### Requirement: Due resolution is explicit and idempotent

到期值守 SHALL 通过配置的 PhysicalDice 请求结算。死亡或退休猎人的到期岗位 SHALL 无需桌面骰、零收益清理；正常结算 SHALL 以人口饱和规则应用结果并移除 active 状态。重复提交或无效骰点 SHALL 不重复写入。

#### Scenario: Due duty resolves exactly once

- **WHEN** 一个有效到期岗位收到匹配请求的合法 PhysicalDice 结果
- **THEN** 人口结果 SHALL 应用一次并移除该 active assignment
- **AND** 重复提交同一 AssignmentId SHALL NOT 再次修改人口

### Requirement: Departure gate

存在到期岗位时营地 SHALL 拒绝整体出猎；被派驻猎人 SHALL 不能进入出猎名册。准备阶段和最终名册提交 SHALL 都执行门禁。

#### Scenario: Due duty blocks departure

- **WHEN** 营地仍有一个未结算的到期岗位
- **THEN** 出发准备与最终名册提交 SHALL 均被拒绝
- **AND** Campaign SHALL 保持在 Settlement

### Requirement: World-space interaction

设施值守 SHALL 通过营地世界空间入口、岗位卡和猎人卡显式选择岗位与猎人。面板关闭、重绑或禁用后，迟到异步结果 SHALL 不修改新表现或再次提交；纯表现 SHALL 不成为权威状态。

#### Scenario: A stale panel continuation is ignored

- **WHEN** 玩家关闭或重绑值守面板后旧异步选择才返回
- **THEN** 旧面板 SHALL NOT 再次提交值守命令
- **AND** 新表现世代 SHALL 保持不变

### Requirement: Persistence compatibility

值守状态 SHALL 使用独立 schema 字段。旧存档 SHALL 初始化/迁移缺失集合，未来 schema 或无法解析的活动引用 SHALL fail closed；成功取消或结算后 active 列表 SHALL 移除该 AssignmentId。

#### Scenario: Future duty schema fails closed

- **WHEN** 存档携带高于当前支持版本的值守 schema
- **THEN** 候选营地恢复 SHALL 被拒绝
- **AND** 原存档 SHALL NOT 被降级或改写

## Implementation evidence

- `Assets/GameScripts/GameLogic/HuntingInDarkness/GameCore/Settlement/SettlementFacilityDuty.cs` contains immutable definitions, absolute due coordinates and saturated population rules.
- `Assets/GameScripts/GameLogic/HuntingInDarkness/Adapters/Unity/ContentTables/PlayableFacilityDutyTable.cs` loads and validates `facilities.json`; the production `shelter_watch` row uses PhysicalDice 1d6 and bands 1-2/3-5/6.
- `Assets/GameScripts/GameLogic/HuntingInDarkness/Adapters/Unity/ActionFlow/Settlement/SettlementFacilityDutyActions.cs` owns assignment, cancellation and due resolution through ActionQueue events.
- `Assets/GameScripts/GameLogic/HuntingInDarkness/ViewLayer/Settlement/Table3D/SettlementFacilityDutyPanel3D.cs` and the facility/hunter cards provide the world-space selection surface with generation guards.
