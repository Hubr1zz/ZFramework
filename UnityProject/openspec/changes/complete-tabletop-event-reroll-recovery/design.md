## Context

`ResolvePlayableEventNodeAction` 已在营地与狩猎 ActionQueue 中复用同一选择、随机表现、重投支付和效果提交链。重投成功后，`PlayableEventChoiceTransaction` 会立即扣除 1 点意志、增加 1 点命运，并发布 `Reroll` 提交事实；阶段保存因此能保留角色数值，但两种 occurrence 都缺少继续事务所需的选项、骰值与已重投标记。

## Goals / Non-Goals

**Goals:**

- 把一次已支付重投变成 occurrence 的可恢复子状态，并保持营地和狩猎共用同一恢复协议。
- 恢复后直接展示已重投结果，接受时只提交一次最终效果与事件记忆。
- 对内容漂移、行动者失效、数值越界和未知版本 fail closed。
- 保持 3D View、物理骰 presenter、ActionQueue 和阶段持久化的现有职责。

**Non-Goals:**

- 不允许额外重投次数，不新增重投数值或事件内容。
- 不重构 GameManager、阶段管理器或 ActionQueue 基类。
- 不把动画、骰子稳定等待或按钮点击写入权威检查点。
- 不推进 Showdown。

## Decisions

- 新增小型可序列化 `PlayableEventRerollCheckpoint` 数据对象，保存 schema、EventId、OptionId、ActorId、RollValue 与冻结 Bonus。使用稳定 OptionId 而非列表下标，避免内容表重排改变分支身份。
- `PlayableEventChainOccurrence`、营地 occurrence/Timeline 条目和活动狩猎 occurrence 快照只持有该可选对象。没有该对象的旧档继续走原入口；有对象时必须完整校验后才能创建已重投事务。
- `EventSystem` 提供窄的恢复工厂，重验稳定选项身份、行动者世代和骰值范围；已支付的选择不重新执行可用性门禁，避免资源或状态变化否定已经提交的重投。`PlayableEventChoiceTransaction` 只增加内部恢复入口，不新增第二套提交逻辑。
- `ResolvePlayableEventNodeAction` 收到恢复快照时跳过选择和首次随机表现，从已重投结果进入现有 3D check 卡；`CanReroll` 因 `HasRerolled=true` 保持关闭。
- Settlement 在 `Reroll` checkpoint 写回当前 Timeline 或 pending occurrence；Hunt Store 更新当前 occurrence，并由既有活动狩猎快照捕获。两者均先写 checkpoint，再发布现有阶段提交事实。
- 新增字段采用向后兼容空值，并用显式 `HasValue` 区分 Unity `JsonUtility` 生成的默认嵌套对象与真实检查点；当前无需提升外层 DTO 版本。标记存在但内容无效时仍 fail closed。

## Risks / Trade-offs

- [事件内容在存档后删除或修改选项] → 以 EventId、OptionId、行动者和骰值范围重验，失败时保留 occurrence 并保持流程门禁。
- [重投支付已保存但 checkpoint 写入失败] → 阶段 handler 只允许更新当前仍 pending 的 occurrence；失败转为可诊断 Action 失败，不提交事件结果。
- [恢复时角色属性已变化] → 使用 checkpoint 冻结的 Bonus 还原当时结果，避免重算改变已经支付的判定；最终效果仍通过当前权威端口预检。
- [共享测试文件导致多个 Spec 证据整文件哈希失效] → 新恢复测试优先放入职责专属测试文件，生产 3D 竖切只在必要时扩展现有 PlayMode 文件。

## Migration Plan

- 旧营地 occurrence、Timeline 与活动狩猎快照缺少重投对象时按“未发生重投”读取。
- 新对象存在但 schema 或身份不受支持时拒绝恢复且保留原载荷。
- 回滚代码不会修改旧字段；但包含新重投对象的存档不得由不认识该契约的旧版本继续执行。

## Open Questions

无。首期仅支持现有每个事件 occurrence 一次重投规则。
