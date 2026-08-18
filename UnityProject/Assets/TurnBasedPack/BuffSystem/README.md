# BuffSystem

一个不引用 Unity、ActionQueue、UI 或特定战斗规则的 Buff 生命周期与数值公式核心。

## 模块边界

```text
Runtime/Core                 Buff 身份、归属、层数、持续时间、标签、合并和触发次数
Runtime/Formula              可配置公式、参数区域、Modifier 层与 Buff 数值绑定
ActionQueueAdapter           可选：Buff 生命周期 <-> Entity Reactor / GameAction
Tests/Editor                 生命周期、公式和可逆贡献测试
```

Buff 是持久状态，Reactor 是 Action 事件响应能力。Buff 可以通过 Adapter 拥有 Reactor；
一次出牌等局部流程也可以直接使用临时 Reactor，而不制造一个没有状态语义的 Buff。

## Buff 生命周期

`BuffDefinition` 是共享定义，`BuffInstance` 是运行时实例，`BuffContainer` 管理实例。
支持以下策略：

- `Independent`：同名实例独立存在；
- `Reject`：已存在时拒绝；
- `Stack`：只叠层；
- `RefreshDuration`：只刷新持续时间；
- `StackAndRefreshDuration`：叠层并刷新；
- `Replace`：移除旧实例后添加新实例。

超出这些常见规则时，可以给定义注入 `IBuffMergeStrategy`，决定拒绝、独立添加、更新或替换。
移除会先触发 `Removing`，并携带 `Explicit`、`Dispel`、`Replaced` 或 `Expired` 原因；规则层可按原因拒绝，
例如“不可驱散但允许自然过期”。

持续时间不绑定“回合”或 `deltaTime`。游戏显式推进命名时钟：

```csharp
var turns = new BuffClock("TurnEnd");
container.Advance(turns, 1);

var seconds = new BuffClock("GameSeconds");
container.Advance(seconds, deltaTime);
```

永久 Buff 的 Duration 为 `null`。

## 可配置公式与乘区

公式结构和 Modifier 层是两个正交概念：

1. 表达式定义参数如何组合；
2. 每个参数配置自己的 Modifier Pipeline；
3. Modifier 明确声明 `Formula + Parameter + Layer`。

例如 `a * (b + c * d) + e`：

```csharp
var expression = new FormulaParser().Parse("a * (b + c * d) + e");
var formula = new FormulaDefinition("Damage", expression)
    .ConfigureParameter("a", new ModifierPipeline()
        .AddLayer(new ModifierLayerKey("Multiply"), ModifierReducers.Multiply))
    .ConfigureParameter("c", new ModifierPipeline()
        .AddLayer(new ModifierLayerKey("Flat"), ModifierReducers.Add));
```

内置 Reducer 有 `Add`、`AdditiveMultiplier`、`Multiply` 和 `OverrideByPriority`；
特殊游戏规则可通过 `ModifierReducers.Custom` 定义。表达式也支持自定义函数节点。
`FormulaParser` 可注册项目自定义函数，因此公式文本可以来自 JSON、ScriptableObject 或表格；
解析应在配置加载阶段完成，不应在每次伤害计算时重复进行。

`BuffStatModifierCatalog` 单独维护 `BuffKey -> StatModifierTemplate[]`，因此 Buff 定义本身
不依赖某套数值公式。`BuffStatBinding` 在 Buff 添加、叠层和移除时创建或释放精确的
Modifier Handle。

## UI

核心不保存图标、描述、是否隐藏等 UI 字段。展示层可以订阅 `BuffContainer.Changed`，再通过独立的
`BuffKey -> ViewDefinition` Catalog 判断哪些 Buff 可见以及如何显示。隐藏 Buff 因而不会污染核心模型。

## ActionQueue Adapter

`BuffReactorCatalog` 将 BuffKey 映射为 Reactor 工厂。`BuffActionQueueBinding` 在 Buff 存活期间
注册实体 Reactor，并在 Buff 移除、过期或 Binding 销毁时释放注册。
Reactor 在 Buff 添加时只创建一次，并持有/读取同一个 `BuffInstance`；叠层不会重建 Reactor，
避免丢失次数、冷却等运行时状态。

对 Buff 的添加、移除和时钟推进若需要进入战斗因果链，使用：

- `ApplyBuffAction`
- `RemoveBuffAction`
- `AdvanceBuffClockAction`

Adapter 是可选层；`BuffSystem.Runtime` 的程序集没有 ActionQueue 引用。

需要实现“拥有该 Buff 时攻击不触发反击”等选择性屏蔽时，使用 `BuffReactionGateCatalog` 和
`BuffReactionGateBinding`。它们只注册玩法 `ReactionGate`；不可被玩法屏蔽的基础设施检查属于
ActionQueue 的 `ActionEngineGuardSet`，Buff Adapter 无法访问或移除它。

## 回调限制

`BuffContainer` 生命周期回调中不允许同步再次修改同一 Container。需要派生新 Buff 时，宿主应通过
ActionQueue、事件循环或自己的命令队列延迟执行。这样可避免遍历失效和难以证明的嵌套生命周期。
