# 营地桌面症状成长

## Purpose

把“接受弱点、克服弱点”的核心成长交互迁移到 3D 猎人桌面，并由 Settlement ActionQueue 统一提交长期状态。

## Requirements

### Requirement: 症状以猎人实体卡呈现

拥有未克服症状的猎人 SHALL 在装备桌显示“症状”入口；症状板 SHALL 使用分页实体卡展示名称、内化进度与当前状态。

#### Scenario: 猎人没有症状

- **WHEN** 猎人没有已配置且未克服的症状
- **THEN** 装备桌 SHALL 隐藏症状入口，症状板不得产生空白可点击卡

### Requirement: 玩家明确选择内化或克服

选中症状卡后，桌面 SHALL 分别展示内化与克服的费用、条件和不可用原因；View SHALL 只提交猎人 ID、症状 ID 与选择。

#### Scenario: 本年已经面对症状

- **WHEN** 当前症状的最后面对年份等于营地年份
- **THEN** 内化按钮 SHALL 禁用并说明本年已经面对过该症状

#### Scenario: 已内化但尚未克服

- **WHEN** 症状已经内化且尚未克服
- **THEN** 内化 SHALL 不可重复，克服仍 SHALL 按胆识与成长条件判断

### Requirement: 症状变化由 Settlement Runner 掌权

Settlement ActionQueue SHALL 在执行时重新核对猎人归属、症状内容、持久状态、年份与费用；成功后 SHALL 发布症状事实和统一营地事务事实。

#### Scenario: Reactor 改写选择

- **WHEN** Before Reactor 将内化改写为克服
- **THEN** Action SHALL 按克服条件重新验证并只提交克服结果

#### Scenario: Reactor 阻止提交

- **WHEN** Before Reactor 阻止症状 Action
- **THEN** 意志、成长、属性、症状状态与事务事实 SHALL 全部保持不变

### Requirement: 正常流程不创建旧屏幕症状窗口

正式 `PlayableGameBootstrap` SHALL 不再实例化 `PlayableSymptomGrowthView`；症状长期状态仍 SHALL 兼容现有存档与内容目录。
