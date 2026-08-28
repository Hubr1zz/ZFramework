## Context

物品效果、普通部位伤势、狩猎携带物和活动检查点已经分别存在。缺失的是一条由 3D 玩家意图进入 Hunt runner 的权威命令链，而不是另一套治疗规则或存档模型。

## Decisions

### 只扩展当前 Hunt 交互租约

`IHuntExplorationPort` 组合 `IPlayableHuntConsumableInput`，旧世界物件只能使用当前 session lease。View 提交猎人 ID、物品 ContentId 和部位；不持有 ActionEnvironment、HuntManager 写接口或存档端口。

### 当前行动猎人只能治疗自己

首期不声明物品传递、远程治疗或目标选择规则。Action 执行时要求 owner 仍是当前选中且存活可用的猎人，物品仍在其 Collectibles 中，并且目标部位可恢复。

### 内容世代与扣除在 Action 内失败关闭

物品必须由本次 Hunt 绑定路线解析为 canonical 对象，兼容测试环境才使用当前物品 Registry。相同 ContentId 的旧世代对象、无效数量、未知效果、非零 HuntNoise 或不支持的效果均不提交。扣除和 `HunterRecoveryRules.TryRecover` 在无 await 的执行段完成，意外失败会恢复原堆叠。

### 3D 面板不建立 UI ActionQueue

Consumable 卡只负责打开世界空间部位卡。面板提交期间禁用部位卡并拒绝重新打开；成功后通过玩法事实和命令结果重读权威携带物与生命。普通资源、武器和防具携带卡继续只读。

### 复用现有活动狩猎快照

快照已经保存每名猎人的身体状态与 Collectibles。成功 root 只触发现有 checkpoint 回调，不增加 pending consumable、schema 或恢复重放协议。

## Risks / Trade-offs

- 当前只支持 `RecoverBodyPart`；新效果族必须先定义独立规则和 Action，不得让 View 解析文案。
- 活动检查点沿用现有异步持久化边界，本 Change 不承诺磁盘 IO 与领域提交可回滚。
- 遭遇交接仍按现有门禁暂停 Hunt 命令；本 Change 不扩大 Showdown 生命周期。
