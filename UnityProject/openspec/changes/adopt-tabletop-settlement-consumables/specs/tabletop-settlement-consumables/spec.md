---
schemaVersion: 2
category: feature
title: 营地桌面消耗品
---

## ADDED Requirements

### Requirement: Consumables are validated table-driven content

消耗品 SHALL 使用稳定 ContentId、显式效果类型与正数效果量进入营地内容计划。当前 `RecoverBodyPart` 内容 SHALL 只恢复普通部位生命且 HuntNoise 必须为零；未知效果、无效数值或非消耗品携带效果配置 SHALL 使整批内容失败关闭。

#### Scenario: The medical workshop produces a poultice

- **GIVEN** Tools 已掌握、`medical_workshop` 已建成且原料充足
- **WHEN** 玩家从实体配方卡制作“菌肉敷剂”
- **THEN** 配方 SHALL 原子消耗 `mushroom_flesh` 与 `viscous_sap`
- **AND** 一张稳定 ID 为 `mushroom_flesh_poultice` 的 Consumable SHALL 进入营地物品仓库

#### Scenario: A consumable effect record is invalid

- **WHEN** 内容缺少效果、效果量不在 1 至 99、声明未知效果或携带非零 HuntNoise
- **THEN** 营地内容计划 SHALL 拒绝该批内容
- **AND** 不得发布部分可用的物品或配方对象图

### Requirement: Consumables never become equipment

只有 Weapon 与 Armor SHALL 能进入猎人装备槽。现有 `EquipmentStorage` 持久字段 MAY 作为兼容的非资源物品仓库继续保存装备和消耗品；旧存档中错误出现在装备 ID 列表内的已注册 Consumable SHALL 被移除并恰好返还一张仓库卡，重复恢复不得重复返还。

#### Scenario: A consumable card is dropped toward equipment

- **WHEN** 玩家或兼容调用尝试装备已注册且有库存的 Consumable
- **THEN** 装备 Action SHALL 拒绝请求
- **AND** 库存、运行时装备实例与稳定装备 ID SHALL 保持不变

#### Scenario: Legacy data contains an equipped consumable

- **WHEN** 读档投影恢复一个把 Consumable 写入 `EquippedItemIds` 的旧状态
- **THEN** 该 ID SHALL 从装备列表移除并向物品仓库返还一件
- **AND** 再次执行恢复 SHALL NOT 再增加库存

### Requirement: Consumable use is a physical world-space interaction

3D 猎人装备桌 SHALL 在分页物品仓库中显示 Consumable 实体卡和独立使用槽。拖卡到使用槽 SHALL 只打开该猎人的四张实体身体部位卡；关闭、非法落点或尚未选择目标时 SHALL NOT 消耗物品或修改猎人状态。

#### Scenario: The player selects a wounded body part

- **WHEN** 玩家把菌肉敷剂拖入使用槽并选择一张可恢复的部位卡
- **THEN** View SHALL 禁用重复输入并提交一次包含猎人、canonical 物品与部位的使用命令
- **AND** View SHALL NOT 直接修改库存或生命值

#### Scenario: No body part can receive the effect

- **WHEN** 猎人没有普通伤势，或选择的部位已经满生命
- **THEN** 对应部位卡 SHALL 禁用并说明原因
- **AND** 消耗品 SHALL 保留在仓库

### Requirement: Settlement ActionQueue owns consumable state changes

消耗品使用 SHALL 作为当前 Settlement runner 的 root Action 串行执行，并在写入前重验猎人归属与可用性、canonical 内容、效果、目标部位、库存和取消状态。成功 SHALL 原子扣除一件物品、通过 `HunterRecoveryRules` 应用效果，并依次发布 `HunterConsumableUsedEvent` 与一个 Consumable 事务提交事实。

#### Scenario: Two requests compete for the last copy

- **WHEN** 两个使用请求进入同一 Settlement runner 且仓库只有一件目标消耗品
- **THEN** 最多一个请求 SHALL 成功恢复生命并消耗库存
- **AND** 另一个请求 SHALL 失败且不得产生重复效果或提交事实

#### Scenario: A reactor prevents consumable use

- **WHEN** BeforeExecution Reactor 阻止使用，或命令在提交前取消
- **THEN** 生命、库存与 Outbox SHALL 保持不变

### Requirement: Consumables reuse settlement lifecycle and persistence

消耗品内容 Adapter SHALL 随当前 Settlement ActionSession 创建和释放，不新增平行全局运行态或 MonoBehaviour 玩法权威。成功事务 SHALL 使用现有营地保存边界持久化猎人生命与物品仓库，并且不改变当前存档 schema。

#### Scenario: A successful use reaches the settlement save boundary

- **WHEN** Consumable 事务提交事实被现有组合根接收
- **THEN** 可见装备桌和部位状态 SHALL 从权威数据刷新
- **AND** 下一份营地快照 SHALL 包含恢复后的生命与剩余库存
