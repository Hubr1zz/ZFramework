## Context

事件节点已在 ActionQueue 的 Resolution checkpoint 提交效果与链状态，但结果只存在于内存；Timeline 只记录完成态。事件可形成子链，且触发事件未必拥有 Timeline 条目，因此结果不能只嵌入 `AnnalEntry`。

## Goals / Non-Goals

**Goals:**

- 以稳定身份保存每个已提交营地事件节点的结构化结果。
- 让 Timeline 根 occurrence 精确链接其记忆，并让 3D 年鉴展示所有已提交节点。
- 兼容旧档并拒绝未来 schema；不把本地化文案当作规则身份。

**Non-Goals:**

- 不保存 Hunt 事件记忆，不改变 Calendar、GameManager 或 Showdown。
- 不增加 UI ActionQueue，不实现美术、音效或演出。

## Decisions

- `SettlementInstance.EventMemories` 独立保存根、子链及触发事件；`AnnalEntry.ResolutionMemoryId` 只链接 Timeline 根节点。相比把结果全部塞入 Timeline，这能保持正确基数。
- `MemoryId` 使用事件链稳定身份与 occurrence sequence；相同 ID/相同事实幂等，不同事实 fail-closed。Settlement 保存深拷贝，调用者不能事后篡改权威事实。
- Resolution checkpoint 携带不可变结果事实；取消和判定表现失败发生在此前，不写记忆，结果确认失败发生在此后，不回滚已提交玩法事实。
- 事件选项使用显式 `optionId`；正式营地内容要求节点内非空且唯一。选项索引和显示文本不参与身份。
- 年鉴根据枚举、稳定内容 ID 和前后值格式化玩家文案；snapshot 文本只用于内容缺失时降级。

## Risks / Trade-offs

- [旧档没有历史结果] → 保持原“已发生”展示，不猜测选择或判定。
- [子链 checkpoint 达到容量上限] → 父效果、Timeline、记忆和提交事实仍保持一致，diagnostic 只阻止继续子链。
- [未来新增效果类型] → 保存稳定类型/目标/状态变化，未知类型降级显示稳定 ID。

## Migration Plan

schema 0 初始化空记忆集合并升级为 1；已完成旧条目不补造结果。高于当前 schema 的存档保留原数据并拒绝进入可写营地流程。

## Open Questions

无。本 Change 只等待人类审核后同步正式 Spec。
