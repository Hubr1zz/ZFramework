## ADDED Requirements

### Requirement: The departure launcher displays authoritative blocking reasons in world space

营地出猎入口 SHALL 在打开编队桌前查询当前 Campaign/Settlement 权威门禁。回营恢复、事件恢复、阶段、session 和 in-flight 原因 SHALL 原样投影到世界空间门禁卡；View SHALL NOT 复制这些业务门禁。入口预检只提供反馈，最终 `DepartForHuntAsync` SHALL 再次验证全部玩法规则。

#### Scenario: Return or annual event recovery is incomplete

- **WHEN** 玩家点击出猎入口
- **THEN** 3D 桌面 SHALL 立即显示现有恢复原因
- **AND** 阶段、名册、目的地和持久数据 SHALL 不变化

#### Scenario: No hunter is available

- **WHEN** 玩家打开出猎编队桌但没有可用猎人
- **THEN** 编队桌 SHALL 显示既有名册规则原因
- **AND** 不得静默丢弃点击或打开空目的地确认

### Requirement: Departure remains retryable after the gate clears

门禁失败 SHALL NOT 被 View 缓存。恢复完成后再次点击 SHALL 重新查询权威状态；允许打开编队桌或成功出发时 SHALL 清除门禁 transient。重复失败点击 SHALL 只更新同一个 transient notice。

#### Scenario: Pending return save completes

- **GIVEN** 玩家先因回营保存未完成看到门禁卡
- **WHEN** 保存完成且玩家重新发起出猎
- **THEN** 入口 SHALL 重新验证并允许正常流程
- **AND** 旧门禁卡 SHALL 被清除
