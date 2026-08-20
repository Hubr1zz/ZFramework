# 营地桌面成长训练与年鉴

## Purpose

把旧营地屏幕 HUD 中仍有价值的猎人成长、武器训练、年度状态与年鉴阅读迁移到 3D 桌面，并关闭装备、出发和成长的重复交互旁路。

## Requirements

### Requirement: 猎人成长从猎人实体卡进入

玩家 SHALL 从猎人卡打开 3D 装备桌，再通过“成长训练”实体按钮进入对应猎人的成长训练板。

#### Scenario: 猎人拥有待分配成长

- **WHEN** 猎人存在待分配成长点
- **THEN** 成长训练板 SHALL 以胆识卡和知识卡展示当前值、上限及可用状态

#### Scenario: 属性达到上限

- **WHEN** 胆识或知识已经达到配置规则上限
- **THEN** 对应卡 SHALL 禁止提交并解释原因

### Requirement: 成长分配由 Settlement Runner 掌权

成长 View SHALL 只提交猎人与成长方向；Settlement ActionQueue SHALL 串行重验猎人归属、可用状态、成长余额与属性上限，并在提交后发布成长、里程碑与持久化事务事实。

#### Scenario: Reactor 阻止成长

- **WHEN** Before Reactor 阻止本次成长 Action
- **THEN** 猎人属性、成长余额、里程碑和提交事件 SHALL 全部保持不变

#### Scenario: 成长触发里程碑

- **WHEN** 成长后的属性达到未领取里程碑
- **THEN** 里程碑奖励 SHALL 与成长在同一 Action 中写入，并在提交后发布事实

### Requirement: 武器训练使用 3D 流派卡

成长训练板 SHALL 从 `PlayableWeaponMasteryCatalog` 读取流派、费用和经验，并用分页 3D 卡展示；卡牌只提交现有 `TrainWeaponAction`。

#### Scenario: 训练尚未解锁

- **WHEN** 营地尚未掌握训练所需发明
- **THEN** 流派卡 SHALL 展示前置条件且不得提交

#### Scenario: 训练成功

- **WHEN** 猎人、发明、资源和熟练度上限均满足规则
- **THEN** Settlement Runner SHALL 原子扣除资源、增加熟练度并发布事务事实

### Requirement: 年鉴是可随时打开的桌面物件

营地 SHALL 提供年鉴入口卡，显示当前年份、本年狩猎进度与记录数；年鉴板 SHALL 把时间线和狩猎历史合并为按年份排序的分页实体条目。

#### Scenario: 记录超过单页容量

- **WHEN** 时间线与狩猎历史合计超过八条
- **THEN** 年鉴 SHALL 分页且任何条目不得超出面板边界

#### Scenario: 存在未来事件

- **WHEN** 时间线条目尚未完成
- **THEN** 年鉴 SHALL 以不同状态显示“将发生”，保留玩家对未来战役的预期

#### Scenario: 狩猎回营但年份尚未推进

- **WHEN** 狩猎记录已经提交且本年仍可继续出猎
- **THEN** 年鉴入口卡和已打开的年鉴 SHALL 立即显示新的狩猎进度与记录数

### Requirement: 正常流程不创建旧营地屏幕 HUD

正式 `PlayableGameBootstrap` SHALL 不再实例化 `PlayableSettlementHud`。3D 出发端口 SHALL 在没有旧 HUD 的情况下继续由营地桌面调用。

#### Scenario: 进入营地

- **WHEN** 正式组合根进入 Settlement 阶段
- **THEN** 场景 SHALL 存在 3D 年鉴、成长训练与出发端口，且不存在 `PlayableSettlementHud` 实例
