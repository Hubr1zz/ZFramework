---
schemaVersion: 2
category: architecture
title: "营地内容计划生命周期"
---

# Settlement Content Plan Lifecycle Specification

## Purpose

把物品、发明、配方、猎人模板与营地事件的跨表对象图冻结为同一战役级内容世代，使启动预检、正式营地、招募与装备流程复用相同对象身份，并让失败候选可以完整回收。

## Requirements

### Requirement: Plan preparation is side-effect free

营地内容计划 SHALL 在离线准备阶段读取基础资产、扩展包与内容表，创建候选对象并完成跨表校验；准备阶段不得修改当前 Plan、兼容 Registry、Catalog 运行时字段或 SettlementManager。

#### Scenario: A candidate is interrupted after preparation

- **WHEN** Item、Invention 与 Hunter 表对象已经生成，但候选尚未发布
- **THEN** 当前 Plan 与三个兼容 Registry SHALL 保持安装前状态
- **AND** 被拒绝候选拥有的所有 transient Unity 对象 SHALL 被释放
- **AND** 外部序列化资产与事件世代 SHALL 保持有效

### Requirement: Cross-table object graph fails closed

计划 SHALL 拒绝缺少显式稳定身份的 Item、Invention 或 Event，以及重复对象、别名冲突、无效配方引用、发明前置循环、未知主动事件、未知事件资源或发明效果、无效调度事件与计划外子事件链。任一错误 SHALL 拒绝整批计划，不得发布部分可用内容。

#### Scenario: A cross-table reference cannot be resolved

- **WHEN** 配方、猎人装备、发明或事件引用计划中不存在的稳定内容
- **THEN** 计划构建 SHALL 返回诊断并失败
- **AND** 已生成的同批对象 SHALL 被统一回收

### Requirement: Campaign publishes one settlement generation

Campaign 安装 SHALL 在事件世代发布后，以单一 `PlayableSettlementContentPlan` 指针发布 Item、Invention、Recipe、Hunter 与 Settlement Event 对象图。Plan SHALL 持有一个不可变 RegistryBundle，Item、Invention 与 Event 兼容 Registry SHALL 仅作为该 Bundle 的只读门面，不得持有独立索引状态。

#### Scenario: Settlement plan publication succeeds

- **WHEN** 候选计划通过跨表校验并完成安装事务
- **THEN** Item、Invention 与 Event Registry SHALL 通过同一 Bundle 解析该计划的对象
- **AND** 后续创建的多个 SettlementManager SHALL 复用相同内容对象身份
- **AND** 正式路径 SHALL NOT 再次读取表或创建另一批对象

#### Scenario: A legacy registry is reconfigured during an active campaign

- **WHEN** 活动 Plan 已发布后旧代码尝试独立 Configure Item、Invention 或 Event Registry
- **THEN** 配置 SHALL 被拒绝
- **AND** 当前 Bundle 引用、列表及稳定身份解析结果 SHALL 保持不变

### Requirement: Published event generation remains leased

活动 Plan SHALL 租用其 RegistryBundle 引用的同一事件世代。只要该 Plan 未退役，事件表的公开 Rebuild、ClearCache 与内容目录重配置入口 SHALL fail closed，不得销毁或替换 Plan 仍引用的 EventData。

#### Scenario: Event cache maintenance is requested during an active campaign

- **WHEN** 活动 Plan 已发布后调用事件表 Rebuild 或 ClearCache
- **THEN** 操作 SHALL 被拒绝并报告诊断
- **AND** 当前事件世代、Bundle 列表及事件对象身份 SHALL 保持不变
- **AND** Plan 解除活动引用并退役后，显式缓存清理 SHALL 可以释放该世代

### Requirement: Legacy registry configuration swaps one composite binding

没有活动 Plan 的测试或兼容环境 MAY 独立配置某类 Registry，但每次配置 SHALL 构建并交换一个包含 Item、Invention 与 Event 索引的复合 legacy Bundle。RuntimeSnapshot SHALL 捕获和恢复该单一 Bundle 引用，不得分别复制三份 Registry 状态。

#### Scenario: Campaign installation rolls back to a legacy binding

- **WHEN** 安装前没有活动 Plan，但存在兼容 Registry 配置，且候选安装失败
- **THEN** Runtime SHALL 恢复安装前的同一 legacy Bundle
- **AND** 三类兼容门面 SHALL 同时观察到恢复后的对象身份

### Requirement: Plan ownership lasts for the campaign

计划 SHALL 只拥有表生成的 ItemData、InventionData 与 HunterData；EventData 继续由事件世代拥有，外部 ScriptableObject 只被引用。计划只有在解除活动引用后才能退役，退役 SHALL 幂等并尽力释放全部 owned 对象。

#### Scenario: Runtime state is reset

- **WHEN** 活动 Campaign 内容 Runtime 被重置
- **THEN** 当前 Plan SHALL 先从 Registry 与 Runtime 解除
- **AND** 计划拥有的 transient 对象 SHALL 被释放
- **AND** 外部内容资产 SHALL 不被销毁

### Requirement: Future save schemas fail before projection

计划投影 SHALL 在修改 SettlementInstance、Timeline、Workshop 或猎人列表前检查 Item、Invention、Event、Campaign Pacing 与 Settlement Modifier schema。任一 schema 高于当前版本时 SHALL 拒绝投影并保持输入状态不变。

#### Scenario: A future campaign pacing schema is loaded

- **WHEN** 存档的 Campaign Pacing schema 高于当前运行时
- **THEN** 计划投影 SHALL 返回失败
- **AND** 年份、猎人、事件池与其他存档字段 SHALL 不被迁移或初始化

### Requirement: Loaded settlement publishes one complete candidate runtime graph

读档 SHALL 把尚未归属运行态的反序列化 `SettlementInstance` 作为一次性 owned input，在独立随机源与完整 `SettlementManager` 候选图上完成 schema 迁移、内容计划投影和派生数据恢复。候选 SHALL 同时拥有 Data、Timeline、Event、Invention、Workshop 与 HunterManagement；正式组合根 SHALL 只通过一次权威 Manager 引用交换提交该图。

#### Scenario: Candidate projection fails

- **WHEN** 读档候选包含无效 schema、重复持续修正或其他无法投影的内容
- **THEN** 当前权威 Manager、Data、五个子系统、随机序列、会话与阶段 SHALL 保持不变
- **AND** 失败候选输入 SHALL 被视为已经消费，不得重新进入任何运行态

#### Scenario: Candidate projection succeeds

- **WHEN** 候选完成全部迁移并仍绑定当前活动 Plan
- **THEN** 运行时出发准备 token SHALL 被清除，持久出发 token SHALL 保留
- **AND** 装备 SHALL 通过稳定内容身份重建为候选图内的新实例
- **AND** 普通营地读档 SHALL NOT 恢复只属于活动狩猎运行态的 Collectibles
- **AND** 五个子系统 SHALL 引用同一个候选 Data 及其同代协作系统

#### Scenario: A live Data alias or stale candidate is submitted

- **WHEN** 调用方尝试把当前权威 Data 作为可消费候选重新注入，或提交已经消费、Plan 已退役的候选
- **THEN** 提交 SHALL 被拒绝
- **AND** 当前运行图与 Data 字段 SHALL 保持不变

### Requirement: Load commit waits for idle action environments

GameManager SHALL 在文件读取完成后、候选准备与提交前检查 Settlement、Hunt 与 Campaign ActionEnvironment，以及出发和回营 in-flight 门禁。任一旧流程仍在运行时 SHALL 拒绝读档，避免旧命令跨代写入新战役。

#### Scenario: An action starts while the save file is loading

- **WHEN** LoadAsync 返回时任一受管 Runner 或远征交接仍在执行
- **THEN** GameManager SHALL 拒绝替换权威运行图
- **AND** 当前与候选数据 SHALL NOT 因本次读档发生跨代写入

### Requirement: Active hunt restore observes the candidate generation

活动狩猎恢复 SHALL 使用候选 Settlement Data 解析猎人引用并预生成稳定 payload。在阶段切换通知发出前，GameManager SHALL 同时发布候选 SettlementManager 与 HuntManager，使同步观察者只看到 Hunt 阶段及其同代对象图；会话与表现初始化成功后才释放旧 idle 会话。

#### Scenario: Active hunt snapshot is restored

- **WHEN** 候选营地图、狩猎快照和稳定 payload 都验证成功
- **THEN** HuntManager 的活动猎人 SHALL 与候选 Settlement Data 中的猎人为同一对象
- **AND** Hunt 阶段同步观察者 SHALL 读取候选 Manager 图
- **AND** 失败的阶段切换或会话初始化 SHALL 尝试恢复旧运行图并报告任何阶段回滚失败

## Known Boundary

Plan 与三个兼容 Registry 已收敛为单一不可变 RegistryBundle；公开 Configure 只服务没有活动 Plan 的旧测试/兼容环境。Bundle 冻结成员、索引键与对象身份，但不会克隆外部 ScriptableObject；发布后修改内容资产不属于受支持的运行期操作。候选输入采用明确的消费式 ownership，不提供通用深拷贝。核心运行图通过 Manager 单引用交换提交；会话和表现仍在提交后重绑，当前验证以 EditMode 数据与组合关系为主，尚未加入完整 GameManager PlayMode 视觉 smoke。
