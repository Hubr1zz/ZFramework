# TurnBasedPack 全生命周期接入评估

更新日期：2026-08-18

## 决策

采用 TurnBasedPack 的 `ActionQueue + Reactor` 作为全游戏的因果执行底座，并在大量事件、物品、装备与流程覆盖内容进入项目之前先完成基础迁移。

不采用唯一全局 Runner。战役、营地、狩猎、战斗分别维护自己的 Action 执行环境和 Reactor 池，由战役协调器负责跨环境编排。这样既能共享同一套 Action 语义，又能让规则注册、取消、调试预算和释放边界与功能生命周期一致。

当前阶段只完成评估与迁移门槛定义，不把既有流程零散接入新队列。旧 `GameCore/Cards/ActionQueue` 与 TurnBasedPack 不得共同执行同一个 Root Action。

## 第一性约束

游戏需要解决的不是“排队调用函数”，而是以下五类权威问题：

1. 一个玩家意图从验证、付费、判定、状态改变到表现完成，必须存在唯一且可追踪的因果顺序。
2. 装备、Buff、Boss 规则、事件和设施能在明确作用域内阻止、改写或注入流程，而不污染其他会话。
3. 阶段退出或会话销毁后，旧异步行为和 Reactor 不得继续改变新环境。
4. UI 与其他系统只观察已提交事实，不通过普通事件监听器偷偷修改权威状态。
5. 失败、取消、循环和部分提交必须有明确语义，不能由调用方猜测。

TurnBasedPack 已提供 Root FIFO、非递归注入、Reactor 优先级与作用域、ReactionGate、取消、循环预算及显式 Outcome，适合作为上述底座；跨 Runner 协调、事务提交、事件出站和持久化仍需项目层补齐。

## 目标结构

```text
ZFramework Procedure / PlayableGameBootstrap
                    │
          CampaignFlowCoordinator
          ┌─────────┼──────────┬──────────┐
          │         │          │          │
   CampaignEnv  SettlementEnv  HuntEnv  CombatEnv
   战役生命周期    阶段生命周期   单次远征    单场战斗

每个 ActionEnvironment 拥有：
Runner / Engine + ReactorRegistry + ReactionGates
+ EntityHandleRegistry + Lifetime CancellationToken
+ EventOutbox + 可选 PresentationDispatcher
```

建议的环境职责：

- `CampaignActionEnvironment`：年份推进、阶段计划、结局、保存边界与跨领域协调，随战役存活。
- `SettlementActionEnvironment`：招募、休养、建设、训练、事件选择，离开营地阶段时释放。
- `HuntActionEnvironment`：移动、翻开、采集、狩猎事件、遭遇，随一次 `HuntSession` 创建和释放。
- `CombatActionEnvironment`：卡牌、费用、判定、伤害、状态、死亡和胜利，随一次 `CombatSession` 创建和释放。

不同环境可配置不同的循环预算、等待策略、Reactor 集合和表现分发器，但应复用同一个项目级 `IActionEnvironment` 契约与诊断格式。

## Action、Reactor 与事件的边界

- `GameAction` 是命令或因果步骤，唯一允许提交权威状态改变。
- `Reactor` 是同一执行环境中的规则覆盖与流程注入，例如护甲减伤、装备追加效果、Boss 反击或设施折扣。
- TEngine `GameEvent` / 当前 `EventBus` 是提交后的不可变事实，用于 UI、音效、任务、统计与跨系统通知。
- 普通事件监听器不得承担与 Reactor 重复的权威修改；同一效果只能有一个执行来源。
- 跨环境事实先进入 `CampaignFlowCoordinator`，等源 Root 提交完成后，再向目标环境排入新的 Root Action；禁止跨 Runner 嵌套修改。

建议由环境维护 Event Outbox：Action 成功提交后按顺序发布事实；失败、阻止或取消时丢弃未提交事实。Presentation 只消费事实或 Action 表现请求，不反向承担结算。

## 当前实证

- Unity MCP 数据探针确认两个 Runner 的 ReactorRegistry 相互隔离：Runner A 的目标减伤 Reactor 不会影响 Runner B。
- Engine 探针确认阻止产生 `Prevented`、失败可注入反击、间接循环会被预算终止为 `Failed`。
- BuffSystem 与 PreviewSystem 的 17 个现有 EditMode 测试全部通过。
- ActionQueue 核心目前没有 `[Test]` 自动化测试，只有示例与 Benchmark；不能把示例运行等同于核心回归保障。

## 接入前的实际阻塞

`Assets/TurnBasedPack/ActionQueue` 没有 Runtime asmdef，因此被编入默认 `Assembly-CSharp`；项目 `GameLogic` 使用命名程序集，按 Unity 规则不能引用默认程序集中的类型。Buff 的 ActionQueue Adapter 与 PresentationSystem 也缺少独立程序集边界。

正式引用前应一次完成包级加固：

1. 为 ActionQueue Runtime 增加 asmdef，并为其 Editor 子目录增加 Editor-only asmdef。
2. 为 Buff ActionQueue Adapter 增加引用 Buff Runtime 与 ActionQueue Runtime 的 asmdef。
3. 为 Presentation Runtime 增加 asmdef。
4. 让 `GameLogic.asmdef` 显式引用需要的 Runtime 程序集。
5. 将修复维护为版本化本地 UPM 包或可重复补丁，避免重新导包覆盖。
6. 为核心顺序、作用域、取消、循环预算和 Runner 隔离补最小自动化测试后，再迁移业务。

上述程序集阻塞和最低测试门槛已于本轮解除：ActionQueue Runtime/Editor、Buff Adapter、Presentation Runtime 已有独立 asmdef，`GameLogic` 可显式引用；ActionQueue 新增 5 个核心测试并与相关程序集回归共同通过。包仍位于 `Assets`，版本化本地 UPM 是长期发布门槛，不阻止当前受控迁移。

## 必须由项目层补齐的能力

1. `IActionEnvironment`：统一 Runner、生命周期令牌、实体句柄、Outbox 和释放协议。
2. `CampaignFlowCoordinator`：跨环境编排、阶段切换、保存时点与失败策略。
3. 稳定 `IReactorEntity` 句柄：按 HunterId、ItemId、BossPartId 等稳定 ID 缓存；不能临时重复包装，因为 Reactor 实体匹配采用引用身份。
4. 提交协议：Composite 不会在后续子 Action 失败时自动回滚已完成子项。经济与内容事务应采用“准备不可变计划 → 原子提交”，必要时显式补偿。
5. 保存规则：初期只允许环境空闲或 Root 成功提交后保存；在 Action/Queue 尚不可序列化前禁止中途保存链状态。
6. 取消规范：所有异步 Action 必须观察环境生命周期 `CancellationToken`，否则释放 Runner 也无法保证及时终止外部等待。
7. 预览模型：Preview 不运行真实 Action/Reactor，关键装备和 Buff 必须显式提供模拟规则，确认时仍要重新验证。

项目层基础实现已经位于 `Adapters/Unity/ActionFlow`：

- `ActionEnvironment` 统一 Engine、Reactor、Gate、Guard、生命周期取消和释放。
- `ReactorEntityHandleRegistry` 保证同一环境内稳定引用身份、不同环境之间身份隔离，释放后禁止重新创建句柄。
- `ActionEventOutbox` 每个 Root 独占；成功后按顺序发布 TEngine 事件，失败、取消、阻止或环境释放时丢弃。

需要特别注意：Engine 返回的是 Root Action 的 Outcome，并不会自动把任意 Reactor 注入 Action 的普通 `Failed` 汇总成整个 Chain 失败。会影响提交成败的关键步骤必须成为 Root Composite 的可观察子 Action，或由后续明确的 Chain Commit Policy 汇总；不能让“可能失败但不影响 Root Outcome”的 Reactor Action 暂存权威提交事件。

Buff Gate 还需特别约束：Gate 虽按 Runner 注册，但不会自动按实体路由。每个 Gate 必须核对所属实体/匹配上下文，否则一个角色的 Buff 可能错误抑制同环境中其他角色的反应。

## 一次迁移、分阶段交付

### 阶段 0：包与契约加固

完成 asmdef、核心测试、`IActionEnvironment`、实体句柄 Registry、生命周期令牌和 Event Outbox。此阶段不改玩法结果。

状态：已完成。Unity MCP 已验证正式 ZFramework 入口可启动，Play Mode 中两个环境的同键实体句柄保持“环境内稳定、环境间隔离”，释放成功且控制台无错误。

### 阶段 1：营地最小垂直切片

优先迁移武器训练或工坊建设的一条完整事务：请求、验证、资源计划、原子提交、Reactor 覆盖、事实发布、保存和反馈全部进入一个 Root。旧入口与新入口配置二选一。

营地切片比战斗更适合验证跨游戏通用语义，且能较低成本暴露事务、事件桥与保存边界问题。

状态：武器训练垂直切片已完成。`PlayableSettlementActionSession` 在进入营地时创建独立环境，离开时释放；`TrainWeaponAction` 负责执行时重验、资源扣除、熟练度提交与失败补偿，Before Reactor 可覆盖成本/经验或阻止命令。成功后 Outbox 依次发布资源、熟练度和 `SettlementTransactionCommittedEvent`，由 `GameManager` 统一触发保存与 View 刷新；正式 View 不再调用旧静态训练服务。Unity MCP 回归 227/227 通过，Play Mode 确认正式入口能创建有效营地环境且控制台无错误。

本阶段尚有两个明确边界：`CanTrainWeapon` 只计算基础规则，不能让 Reactor 折扣反向启用原本不可用的按钮；保存由提交事实触发异步任务，磁盘写入失败不会回写当前 Command Outcome。在装备/设施开始广泛修改可用性前应接入 Preview 规则；在 Campaign 协调器落地时应把保存策略、失败反馈与重试纳入跨环境提交协议。

### 阶段 2：完整战斗 Root

迁移一条完整武器攻击：准备输入、费用、命中牌、伤害、部位效果、死亡/胜利与表现等待。随后迁移 Boss 行动。不得只把伤害步骤塞入新队列而保留旧队列控制外层。

状态：玩家行动卡的外层 Root 已完成。`PlayableCombatActionSession` 随单场 `PlayableCombatSession` 创建和释放，拥有独立 Runner、Reactor、Gate 与实体句柄；正式 `TryPlayCardAsync` 已不再调用旧 `CardEffectResolver`。`PlayCharacterCardAction` 统一重验回合/卡牌资格、准备费用与异步效果、原子提交费用、等待表现、提交卡牌状态，并由 Outbox 按顺序发布 CardPlayed、CardFlipped 与 CombatActionCommitted。Before Reactor 可阻止整次行动，其他 Reactor 可在同一因果链注入动作。

攻击的受击部位、结果牌、伤害、部位效果和 Boss 胜利目前仍由既有 `AttackPipeline` 在 Root 内部顺序执行；它们已属于同一个 Root 的等待范围，但还不是可单独路由的子 GameAction。因此装备/Buff 目前能覆盖或注入整次行动，却不能通过 ActionQueue Reactor 精确覆盖某一次结果牌或某个部位伤害。完成阶段 2 仍需把这些权威步骤逐项改为 Child Action，并迁移 Boss 行动 Root。

现有费用在交互式攻击效果之前提交；若执行中发生非玩法异常，ActionQueue 无法自动回滚已支付费用或已结算的前序命中。后续拆分 Child Action 时必须明确“玩家取消仍消耗行动”与“系统异常补偿”的不同语义，并以不可变攻击计划或显式补偿处理，不能假设 Composite 自动事务化。

### 阶段 3：狩猎与剩余营地流程

迁移移动/翻开/采集/事件/遭遇，以及招募、休养、年度事件等。跨环境结果统一交给 Campaign 协调器产生新 Root。

### 阶段 4：收口

删除无调用者的旧 `GameCore/Cards/ActionQueue`、EventBus 权威修改监听器和兼容流程。最后接入 Debugger、Preview 与更完整的 Presentation。

## 每阶段准入测试

- 两个同类 Runner 的 Reactor、Gate、实体与事件互不泄漏。
- Reactor 顺序、注入位置、阻止、失败、取消和循环预算符合约定。
- 会话释放后不再提交状态或发布事实，注册租约全部释放。
- EventBus 只收到已提交事实，失败流程不会出现“UI 显示成功但状态未提交”。
- 消耗资源的 Action 在失败时没有部分扣款；需要补偿的流程有明确测试。
- 旧/新执行入口不能同时处理同一个 Root。
- 连续两次营地/狩猎/战斗会话的数据互不污染。

## 暂不执行的重构

- 当前不直接修改 TurnBasedPack 核心算法。
- 当前不把所有现有 EventBus 事件一次改名或搬迁。
- 当前不为 Action 做存档/回放格式；先把保存限制在安全边界。
- 当前不以单一全局 Runner 简化接线，因为它会造成 Reactor 泄漏、无关流程互相排队和阶段释放困难。
