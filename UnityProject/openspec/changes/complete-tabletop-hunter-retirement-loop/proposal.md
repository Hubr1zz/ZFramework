## Why

狩猎归来已经会判定猎人退休，但玩家此前看不到可读的退休归档，装备归还、名册移除、读档幂等与招募替补也缺少一条生产闭环证据。长期战役可能因此出现“规则已生效、桌面却像什么都没发生”的断层。

## What Changes

- 退休提交返回实际归还装备数，并只为首次权威退休发布可读事实。
- 营地以 3D 实体记录卡说明退休年龄、装备归还与年鉴记录，保持 UI 非权威。
- 退休猎人从 3D 名册移除，归还装备进入既有仓库卡区；读档不重放退休事实。
- 用一个真实 `salt_ward` 退休案例验证保存、恢复、替补招募与下一次出发。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `hunt-return-outcome-checkpoint`: 明确退休装备归还与事实发布的恰好一次语义。
- `tabletop-settlement-hunters`: 明确退休移除与替补加入的增量桌面投影。
- `tabletop-settlement-notices`: 增加可读的 3D 退休归档记录。
- `campaign-persistence`: 明确退休状态与归还装备恢复后不重放事实。

## Impact

只影响猎人成长适配、既有猎人管理系统、营地通知 View 与定向测试。不修改 GameManager、日历、ActionQueue 权威边界、战斗或 Showdown。
