## Why

抽牌、翻牌和抽鬼牌虽然已有统一规则协议，但生产表现器仍以裸 Cube 和按下即触发的代理完成选择，未复用项目 Cards3D 的物理指针语义。拖动手势会被误判为选择，表现器禁用时也缺少明确的交互清理边界。

## What Changes

- 随机卡牌表现改用 `CardView3D` 派生实体，统一短按、悬停和拖动阈值语义。
- DrawCards 每次只开放当前牌堆顶；FlipCards 与 OldMaid 只允许未选牌，揭示后返回稳定牌 ID 与有界数值。
- 禁用、销毁或取消会释放牌桌、材质、输入租约与等待源，后续请求可重新使用同一表现器。
- 用真实 `random_faceless_hand` 营地事件验证实体抽牌到 ActionQueue 效果、记忆和存档提交。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `tabletop-random-interaction`: 明确生产卡牌必须复用 Cards3D 指针语义，并补齐拖动门禁与生命周期清理。

## Impact

只影响 Cards3D 基础手势、桌面随机卡牌表现器、新卡牌 View 和定向 PlayMode 测试。不修改随机规则、事件数值、GameManager、阶段管理器、日历、骰子或 Showdown。
