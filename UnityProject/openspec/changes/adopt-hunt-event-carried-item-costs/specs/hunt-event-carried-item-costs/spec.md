---
schemaVersion: 2
category: feature
title: 狩猎事件携带物成本
---

## ADDED Requirements

### Requirement: Event item conditions are actor-scoped

狩猎事件选项 SHALL 能通过稳定 ItemId 与正数阈值声明 `MinimumCarriedItem`。可用性 SHALL 只读取当前事件执行猎人的 Collectibles；队友携带物、Settlement 仓库、装备槽和 Resource SHALL NOT 满足该条件。

#### Scenario: Only the owner carries the required item

- **WHEN** 当前执行猎人携带一件 `weathered_field_dressing`，队友也携带相同物品
- **THEN** 该执行猎人的选项 SHALL 可用并显示其物品需求
- **AND** 将 actor 切换到未携带物品的猎人后，队友库存 SHALL NOT 让选项可用

### Requirement: Item costs are preflighted before result effects

同一事件结果分支中的全部 `RemoveItem` SHALL 按稳定 ItemId 聚合，并在任何结果效果执行前检查合法数量、数值溢出、canonical 非资源物品和 actor 的总可用量。只要分支包含物品成本，其资源变化也 SHALL 在写入前按声明顺序完成模拟预检。任一成本无法支付或资源变化无法结算时，物品、资源、生命和玩法事实 SHALL 全部保持不变。

#### Scenario: Aggregate cost exceeds the actor inventory

- **WHEN** 一个结果先声明资源奖励，再以两个 `RemoveItem` 合计消耗两件物品，但 actor 只有一件
- **THEN** 整个结果 SHALL 失败关闭
- **AND** 先声明的资源奖励 SHALL NOT 被提交

#### Scenario: A later resource reward would overflow

- **WHEN** actor 能支付一件物品成本，但同分支稍后的资源奖励会超过可结算整数范围
- **THEN** 分支 SHALL 在扣除物品前失败关闭
- **AND** 物品、资源与物品变化事实 SHALL 全部保持不变

### Requirement: Successful costs mutate only the event actor

成功的 `RemoveItem` SHALL 只从事件执行猎人的 Collectibles 扣除聚合数量，并发布既有 actor-scoped 物品变化事实。队友携带物、Settlement 仓库和装备 SHALL 保持不变，事件 SHALL 继续由既有 `ResolvePlayableEventNodeAction` root 串行执行。

#### Scenario: The actor spends a field dressing during worm rain

- **WHEN** 当前执行猎人选择虫雨中的包扎布选项且携带一件 `weathered_field_dressing`
- **THEN** 该猎人的包扎布 SHALL 减少一件并获得两份 `earthworm`
- **AND** 队友同名物品与 Settlement 仓库 SHALL 保持不变

### Requirement: Hunt content rejects unstable item references

Hunt 内容包 SHALL 在绑定事件世代与物品 Registry 时验证 `MinimumCarriedItem`、`AddItem` 与 `RemoveItem` 的目标为 canonical 稳定 ContentId，且对应物品不是 Resource。未知 ID、显示名别名、旧世代对象或 Resource 引用 SHALL 在开始 Hunt 前失败关闭。

#### Scenario: A table uses the item display name

- **WHEN** 狩猎事件使用显示名而非 Registry 中的稳定 ContentId 声明携带物条件或成本
- **THEN** Hunt 内容包 SHALL 拒绝该内容世代
- **AND** 任何路线 SHALL NOT 租用部分绑定的内容

### Requirement: First release excludes transfers and equipment costs

首期 SHALL 只支持当前事件执行猎人携带的非资源物品成本。能力 SHALL NOT 隐式实现跨猎人代付、物品转移、Settlement 仓库支付、装备耐久或 Showdown 成本；同一结果中的 `RemoveItem` 与 `KillHunter` 组合 SHALL 被内容校验拒绝。

#### Scenario: Another hunter could afford the cost

- **WHEN** 当前执行猎人没有所需物品但另一名猎人能够支付
- **THEN** 选项 SHALL 保持不可用或结果失败关闭
- **AND** 另一名猎人的物品 SHALL NOT 被扣除
