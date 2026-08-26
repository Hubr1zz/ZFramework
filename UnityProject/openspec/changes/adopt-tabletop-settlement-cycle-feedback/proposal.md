## Why

季节推进、年度事件恢复和再次出猎门禁已经是权威玩法流程，但玩家在同年回营后看不到当前季节，且出猎入口被恢复门禁拒绝时可能静默无响应。缺口属于世界空间反馈，不需要修改 Calendar、ActionQueue 或存档。

## What Changes

- 每次成功回营只用信息完整的 `HuntCompletedEvent` 生成一张 3D 归档卡，展示远征季节到新季节/新年度的变化。
- 出猎入口预检复用现有权威门禁，并把具体原因显示为可替换、非阻塞的 3D transient 卡。
- transient 卡立即覆盖当前普通通知，清除后恢复被中断的卡；重复点击不增加队列。
- 目的地 View 不再复制营地流程和猎人业务门禁，最终出发命令仍重新验证全部规则。

## Capabilities

### Modified Capabilities

- `tabletop-settlement-notices`: 增加季节/年度回营归档与可恢复 transient 通知。
- `tabletop-hunt-departure`: 增加权威门禁原因的世界空间反馈与恢复后重试。

## Impact

- `GameManager` 的出猎入口查询、营地 3D notice presenter、目的地 View 和 Campaign loop PlayMode。
- 不改变 Calendar、年度事件、ActionQueue、Campaign persistence、ActiveHunt 或 Showdown。
