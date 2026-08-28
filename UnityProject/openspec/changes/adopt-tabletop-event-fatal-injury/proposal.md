## Why

设计要求猎人在致命伤时展示死亡牌堆、洗牌并由玩家亲手选牌；存活会向牌堆加入死亡牌，死亡则永久退场。现有非决战事件只能直接杀死猎人或添加普通伤势，缺少这条最能体现高风险桌游感的核心流程。

## What Changes

- 新增表驱动 `FatalInjury` 效果，限定 Hunt、选中猎人、稳定死亡牌堆 ID 且独占效果事务。
- 复用 `DeathDeck` 的准备/提交规则，在 3D Cards3D 中展示牌堆构成、背面稳定选位与真实存活/死亡牌面。
- 只有选牌完成后才提交伤势、追加死亡牌或调用唯一猎人死亡事务；取消、阻止和无效结果不修改权威状态。
- 存活结果按稳定 ID 排入专属 `Triggered` 后续事件，并通过独立 occurrence 继承原执行猎人；死亡、非致命伤和阻止路径不排入。
- 增加代表事件“塌落的石板”及存活事件“幸运儿”，并保留无头环境的确定性位置回退。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `table-driven-hunt-events`: 增加配置化且 fail-closed 的致命伤效果。
- `event-driven-hunter-death`: 让死亡牌结果复用唯一永久死亡后果事务。
- `tabletop-random-interaction`: 增加实体死亡牌堆的稳定选位与真实牌面。
- `hunt-session-event-occurrence-recovery`: 明确致命伤在提交前失败时保留 occurrence，提交后不得重放。

## Impact

影响事件数据/读表、Hunt 事件 ActionQueue、事件 occurrence、死亡牌纯规则适配、既有卡牌 Presenter、两个关联代表内容和定向测试。不修改 GameManager、ZFramework 阶段生命周期、`KillHunter`、Combat 或 Showdown。
