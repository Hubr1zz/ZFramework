## Context

项目已有稳定物品身份、工坊制作、营地 ActionQueue、普通部位恢复规则和 3D 装备桌，但 `Consumable` 只有枚举身份。直接创建通用效果系统会在只有一个效果时放大抽象成本；另建库存字段则会引入不必要的存档迁移。

## Decisions

### 首个效果复用普通恢复规则

消耗品表只声明受控的 `RecoverBodyPart` 和数值。Action 仍调用 `HunterRecoveryRules`，因此死亡、退休、满生命和部位合法性保持同一权威规则。当前不开放反射式效果、持续 Buff 或任意脚本回调。

### 现有存储字段作为兼容物品仓库

`EquipmentStorage` 持久字段继续保存全部非资源物品计数，新代码通过通用 item helper 访问；旧 equipment helper 保留为兼容别名。装备规则改为 Weapon/Armor 正向白名单，恢复旧装备状态时把 Consumable 返还同一仓库。

### 使用是玩法命令，选择是 3D 表现

消耗品卡拖入实体使用槽后只打开现有部位卡面板。玩家确认部位时才向 Settlement runner 提交 root Action。Action 在写入前重新校验猎人、canonical 内容、部位、库存和取消状态，成功后依次发布使用事实与事务提交事实。

### 内容与生命周期沿用营地世代

物品效果和生产配方由现有表装配进 `PlayableSettlementContentPlan`，消耗品 Adapter 随 Settlement session 创建，不注册额外 Singleton 或 MonoBehaviour。事务提交继续触发现有刷新与保存边界。

## Risks / Trade-offs

- `EquipmentStorage` 字段名仍偏旧语义；保留它避免存档迁移，后续仅在统一 schema 升级时重命名。
- `HunterRecoveryPanel3D` 现在有休养与消耗品两种明确模式；若未来出现三种以上选目标效果，再评估通用目标选择面板。
- 当前只有一种保守的 1 点恢复消耗品；平衡与更多内容留在数据表扩展阶段。
