---
schemaVersion: 2
category: architecture
title: "狩猎路线入场上下文"
---

# Hunt Route Entry Context Specification

## Purpose

让一次营地出发所选择的冻结 RoutePlan、年份与准备令牌作为 Campaign ActionQueue 命令载荷穿过 Reactor 和阶段提交边界，避免用 `ActiveDestination` 静态选择状态隐式驱动权威狩猎运行态。

## Requirements

### Requirement: Hunt entry carries one exact route identity

Settlement 进入 Hunt 的权威请求 SHALL 携带同一个 `CampaignHuntEntryContext`，其中包含可用 RoutePlan、当前年份与非空出发准备令牌。ActionQueue 与全部 Reactor SHALL 观察同一个 RoutePlan 对象身份，不得只传 DestinationId 后重新解析。

#### Scenario: A Reactor inspects or prevents departure

- **WHEN** 已准备的出猎请求进入 Campaign Runner
- **THEN** Reactor SHALL 观察玩家选择的精确 RoutePlan
- **AND** Reactor 阻止时 Host、当前阶段和活动目的地 SHALL 保持不变

#### Scenario: A legacy Host cannot consume the context

- **WHEN** Hunt 入场请求抵达未实现上下文 Host 接口的旧 Host
- **THEN** Action SHALL fail closed
- **AND** 不得退回只传 GamePhase 的旧入口而丢弃路线身份

### Requirement: Entry validation fails closed at commit time

GameManager SHALL 在提交阶段重新验证请求年份等于当前年份、运行令牌等于持久准备令牌、RoutePlan 仍由当前活动 Bundle 拥有，且按当前目的地解析得到同一对象。任一条件失效 SHALL 阻止进入 Hunt。

#### Scenario: Content or departure preparation changes while queued

- **WHEN** 请求排队后年份、准备令牌或活动 Bundle 已改变
- **THEN** 阶段切换 SHALL 失败
- **AND** 不得创建使用旧路线的活动狩猎

#### Scenario: A synchronous phase listener invalidates the request

- **WHEN** FSM 切换通知期间有订阅者替换营地、年份、准备令牌或活动 Bundle
- **THEN** Hunt 初始化前与路线提交前 SHALL 再次验证同一上下文
- **AND** 失败 SHALL 走阶段回滚，不得发布旧路线

#### Scenario: Another phase tries to enter Hunt

- **WHEN** 当前阶段不是 Settlement，却提交携带路线的 Hunt 入场请求
- **THEN** Action 与 GameManager Host SHALL 双重拒绝请求
- **AND** 不得发布阶段提交事实或创建不完整 Hunt 运行态

### Requirement: Normal entry does not use the compatibility side channel

正常出发 SHALL 先只读解析 RoutePlan，再提交营地准备 Action，随后携带上下文请求阶段切换。`ActiveDestination` MAY 继续服务开发启动与旧恢复入口，但 SHALL NOT 是正常 Settlement→Hunt 的权威输入。

#### Scenario: Prepared departure fails before phase commit

- **WHEN** Campaign Action 被规则阻止或 Host 校验失败
- **THEN** 全局活动目的地 SHALL 不被提前修改
- **AND** 玩家 SHALL 留在 Settlement

#### Scenario: A legacy phase-only API requests Hunt

- **WHEN** Settlement 通过只携带 `GamePhase.Hunt` 的旧入口请求出猎
- **THEN** GameManager SHALL 拒绝该请求
- **AND** 只有显式开发启动 seam MAY 使用兼容目的地回退

### Requirement: Route publication is atomic with Hunt entry

GameManager SHALL 在新 HuntManager 成功绑定同一 RoutePlan 并建立地图后提交活动路线，且在创建 3D 表现或 Hunt ActionSession 前完成提交。后续表现或 Session 初始化失败 SHALL 恢复此前完整目的地运行状态并释放候选 Manager。

#### Scenario: Presentation initialization fails

- **WHEN** RoutePlan 已绑定且活动路线已提交，但 3D 表现或 Hunt Session 创建失败
- **THEN** 先前的 Destination RuntimeState SHALL 被恢复
- **AND** 候选 HuntManager、Session 与表现对象 SHALL 不得作为活动状态残留

### Requirement: Active hunt identity comes from the bound route

活动狩猎 Session、事件/首领遭遇请求与活动存档捕获 SHALL 从 HuntManager 的 BoundRoute 读取 DestinationId，不得从可变的 `ActiveDestination` 读取权威身份。

#### Scenario: Compatibility selection changes during a hunt

- **WHEN** 兼容选择门面在活动狩猎期间被其他代码修改
- **THEN** Session、遭遇交接和存档 SHALL 仍使用 BoundRoute 的目的地身份

## Known Boundary

本阶段仅建立运行期 RoutePlan 入队、验证、提交和回滚契约。活动狩猎持久化仍只保存 DestinationId，尚未保存或校验 ContentBundleId；跨内容版本恢复必须继续视为未完成能力。
