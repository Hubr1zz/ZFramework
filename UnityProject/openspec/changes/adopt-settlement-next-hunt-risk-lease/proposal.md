## Why

营地事件原本只能立即修改营地数据，无法把一次性风险可靠地注入下一次狩猎。直接由 `GameManager` 或 Hunt runtime 读取事件字段会让跨阶段效果绕开 ActionEnvironment 生命周期，并在读档、出发失败和重新创建 Hunt session 时丢失。

## What Changes

- 增加表驱动的营地事件选项效果，用稳定租约把一次风险投影到下一次狩猎。
- Campaign 持有持久效果投影，通过现有 ActionEnvironment installer registry 为当前和未来 Hunt 环境安装 Reactor。
- 下一次狩猎的每次噪音检定获得配置修正；失败出发不消费租约，成功回营权威提交负责清除。
- 租约进入现有营地存档，旧档缺失字段保持兼容，非法租约失败关闭。
- “石像守夜”作为唯一生产案例，保留安全选项和风险收益选项。

## Capabilities

### New Capabilities

- `settlement-next-hunt-risk-lease`: 营地事件对下一次狩猎的一次性玩法风险注入。

## Impact

- 营地事件表、选项事务和 Settlement ActionQueue。
- Campaign 持久效果投影、ActionEnvironment installer registry 与 Hunt 噪音 Reactor。
- 营地存档与成功回营提交边界。
- 不改变 ActiveHunt schema、Showdown、UI 事件或表现层职责。
