---
schemaVersion: 2
category: architecture
title: "狩猎内容 Bundle 生命周期"
---

# Hunt Content Bundle Lifecycle Specification

## Purpose

把默认狩猎地区、可选目的地、地图生成规则、资源引用、事件池与噪音规则冻结为同一战役级内容世代，使启动预检、路线选择和 HuntManager 使用相同对象身份，并阻止旧全局目录在活动战役中静默覆盖路线。

## Requirements

### Requirement: Bundle preparation observes one staged generation

`PlayableHuntContentBundle` SHALL 只使用同一安装事务暂存的 EventGeneration 与 Settlement RegistryBundle 构建默认路线和全部目的地路线。准备阶段 SHALL NOT 读取或发布当前全局事件世代、Hunt Bundle 或目的地选择。

#### Scenario: A candidate is interrupted after Hunt preparation

- **WHEN** 候选已创建 Hunt Bundle，但尚未发布
- **THEN** 当前 Hunt Bundle、事件世代、营地计划与目的地选择 SHALL 保持安装前身份
- **AND** 候选拥有的地块规则快照 SHALL 被释放

### Requirement: Gameplay rules are snapshotted

Bundle SHALL 为起始地块、地图池、资源点配置与噪音配置创建自有快照；集合、生成权重、组规则、Boss 引用、资源抽取规则和噪音牌组标量 SHALL NOT 随来源 Catalog 后续修改而漂移。ItemData 与 EventData SHALL 保持同代 Registry/EventGeneration 中的对象身份，不得跨代复制或重新解析。

#### Scenario: Source assets change after preparation

- **WHEN** 调用方修改来源 Catalog 的列表、地块标量、嵌套资源配置或噪音标量
- **THEN** 已准备 RoutePlan 的成员数量与规则值 SHALL 保持不变
- **AND** Bundle SHALL NOT 销毁来源 ScriptableObject

### Requirement: Stable references fail closed

每条路线 SHALL 拒绝缺少显式稳定 ContentId 的地块或 Hunt 事件、重复地块 ID、无效生成规则、计划外资源引用、路线事件池之外的翻牌事件以及不连续的噪音危险事件覆盖。事件表覆盖 SHALL 按 ContentId 合并，不得按 Unity 对象名称绑定。

#### Scenario: A tile references another content generation

- **WHEN** 地块资源无法在同批 Settlement RegistryBundle 解析为同一 ItemData 对象
- **THEN** 整个 Hunt Bundle 准备 SHALL 失败
- **AND** 不得发布部分路线

### Requirement: Campaign publishes three dependent pointers atomically

战役安装 SHALL 依次准备 EventGeneration、SettlementPlan 与 HuntBundle，再依次发布 EventGeneration、SettlementPlan 与 HuntBundle。失败 SHALL 逆序恢复 HuntBundle、SettlementPlan 与 EventGeneration；成功退役旧内容也 SHALL 先退役旧 HuntBundle，再退役其 Settlement 与 Event 依赖。

#### Scenario: Projection fails after Hunt publication

- **WHEN** Hunt Bundle 已发布但安装探针失败或抛出异常
- **THEN** 三个当前指针 SHALL 恢复为安装前的相同对象
- **AND** 被拒绝 Bundle SHALL 变为不可用
- **AND** 其地块快照 SHALL 被销毁，而旧事件对象 SHALL 保持有效

### Requirement: Published Bundle leases its dependencies

活动 Hunt Bundle SHALL 租用其 EventGeneration。只要 Bundle 未退役，事件缓存 ClearCache 与 Rebuild SHALL fail closed；显式退役 Bundle 后，依赖世代 MAY 在其他租约也释放后销毁。

#### Scenario: Event maintenance runs during an active campaign

- **WHEN** 活动 Hunt Bundle 仍引用当前事件世代
- **THEN** 事件缓存维护 SHALL 被拒绝
- **AND** RoutePlan 的 EventData 引用 SHALL 保持有效

### Requirement: HuntManager binds one route before runtime starts

HuntManager SHALL 通过 `TryBindContent` 全量验证并一次提交 StartingTile、TilePool、HuntEventPool 与 NoiseProfile。相同 RoutePlan 重复绑定 SHALL 幂等；不同计划、已退役计划或已经 OnEnter/TryRestore 的运行态 SHALL 拒绝绑定，且不得部分修改旧配置。

#### Scenario: A second route is applied to a running manager

- **WHEN** HuntManager 已建立地图或恢复活动狩猎后收到另一 RoutePlan
- **THEN** 绑定 SHALL 失败
- **AND** 原 BoundRoute 与全部运行配置 SHALL 保持不变

### Requirement: Completed expeditions do not reuse a manager

正常 Hunt 返回 Settlement 的提交点 SHALL 释放权威 HuntManager 引用。下一次出发 SHALL 创建新 Manager，并且 GameManager SHALL 检查目的地 RoutePlan 绑定结果；绑定失败 SHALL 阻止进入或恢复 Hunt，不得吞掉错误后使用上一条路线。

#### Scenario: The next year selects another destination

- **WHEN** 上一轮狩猎已经提交回营，玩家下一年选择不同目的地
- **THEN** 新 HuntManager SHALL 绑定新目的地的 RoutePlan
- **AND** 地图生成 SHALL NOT 复用上一轮 BoundRoute

## Known Boundary

本阶段完成启动期 Hunt 内容世代、依赖租约、Manager 原子绑定和正常回营后的 Manager 换代。`PlayableHuntDestinationRuntime.ActiveDestination` 仍是兼容选择门面；出发事务尚未把 RoutePlan 作为 `HuntEntryContext` 载荷传递，活动狩猎存档也仍只保存 DestinationId、尚未校验 ContentBundleId。下一阶段 SHALL 让 Campaign transition 与 active-hunt restore 从候选 Bundle 解析并持有精确 RoutePlan；在此之前不得宣称活动 Hunt 已支持跨内容版本恢复。
