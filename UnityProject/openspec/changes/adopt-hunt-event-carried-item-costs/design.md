## Context

事件选择已经由 `ResolvePlayableEventNodeAction` 串行执行，结果事务也有资源成本的预检边界。猎人携带物则由 Hunt session 与活动狩猎快照持有。缺失的是一个不泄露存储结构、可由事件表引用的 actor-scoped 可用量与扣除端口。

## Decisions

### 条件和成本始终绑定事件执行猎人

`MinimumCarriedItem` 与 `RemoveItem` 都使用事件选择时已经确定的 actor。查询和扣除只检查该猎人的 Collectibles；队友、Settlement 仓库和装备槽不作为后备来源。

### 所有物品成本在效果执行前聚合预检

结果分支先按稳定 ItemId 汇总多个 `RemoveItem`，检查溢出、合法数量和总库存；只要存在物品成本，同分支资源变化也按声明顺序模拟并预检，再执行任何效果。物品成本不足或后续资源变化溢出时，整个交换不产生部分变化。

### 事件 root 继续拥有玩法顺序

View 仍只提交事件选项和 actor 意图。物品条件通过组合可用性端口进入既有选择流程，扣除由既有事件 root 执行并发布物品变化事实；不为 UI 建立 ActionQueue。

### 内容包负责跨表稳定引用

事件表只保存字符串 ID。Hunt Bundle 绑定事件世代与物品 Registry 时验证 `MinimumCarriedItem`、`AddItem` 与 `RemoveItem` 的目标是 canonical 非资源物品，拒绝显示名别名、未知 ID 和 Resource。

### 首期只提供一个生产案例

虫雨事件允许当前猎人撕开一件旧式包扎布换取基础资源。跨猎人协作、装备耐久、任意替代成本和物品转移只保留后续扩展空间。

## Risks / Trade-offs

- 当前条件 API 仍组合在事件可用性输入中；若未来成本来源超过角色物品与资源，应引入统一只读成本上下文，而不是继续增加 View 参数。
- 活动检查点沿用现有事件 root 的保存边界，本 Change 不承诺磁盘 IO 与领域提交可回滚。
- `RemoveItem` 与 `KillHunter` 同一结果被拒绝，避免 actor 生命周期与携带物归属在同一事务中产生未定义顺序。
