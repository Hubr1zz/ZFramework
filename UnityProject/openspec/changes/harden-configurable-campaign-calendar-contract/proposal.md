## Why

配置化季节循环已经由生产代码与真实 GameManager 流程实现，但部分正式 Spec 仍保留“一次回营推进一年”的旧契约，且旧事件效果仍可能被内容误认为时间推进入口。需要统一权威语义，并让 3D 年鉴展示战役冻结日历中的季节名称。

## What Changes

- 明确一次首次接受的成功回营只推进一个配置季节，仅越过末季时进入下一年并生成年度事件。
- 明确恢复与旧存档迁移不得猜测或补推进年份。
- 保留旧 `AdvanceYear` 序列化槽位，但禁止事件表、营地内容和运行时把它作为日历写入口。
- 让回营玩法事实携带冻结日历的季节身份与显示快照，3D 年鉴展示活动战役绑定的季节名称。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `campaign-year-loop`: 收紧日历唯一提交入口与配置化季节事实契约。
- `hunt-return-outcome-checkpoint`: 把旧的“一年”提交语义改为“一个配置季节，末季才跨年”。
- `campaign-persistence`: 把回营恢复和旧配额迁移改为保守季节语义。
- `expedition-build-progression-loop`: 把远征奖励闭环的时间结果改为推进一个配置季节。
- `tabletop-settlement-advancement-ledger`: 年鉴显示活动战役绑定的季节名称并在季节提交后刷新。

## Impact

影响营地回营 ActionQueue 事实、事件内容校验、日历纯逻辑兼容入口、3D 回营/年鉴只读表现及其定向测试。不改变存档 schema、CalendarId 冻结策略、GameManager FSM、Showdown 或年度事件生成算法。
