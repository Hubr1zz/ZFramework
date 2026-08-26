## Context

回营 Action 同一次权威提交会发布 `SeasonAdvancedEvent`、`HuntCompletedEvent`，跨年时再发布 `YearAdvancedEvent`。只有 `HuntCompletedEvent` 同时携带完成前后的年/季坐标和远征统计，因此它是“一次回营一张通知”的唯一表现来源。出发门禁则已经集中在 GameManager、恢复投影和最终命令中，View 不应复制。

## Decisions

### 回营通知只消费 HuntCompletedEvent

Presenter 只格式化事件中已有的前后坐标。同年显示季节推进，跨年显示新年度；不订阅另外两类时间事实生成通知，不按季节数量重新计算。

### 门禁提示是非权威 transient

固定 key `hunt-departure-blocked` 更新同一条 3D 卡。它不锁输入、不写存档、不进入 ActionQueue；普通通知被临时覆盖后保留并恢复。入口成功时清除 transient，最终出发命令继续重新验证。

### 具体失败原因保持来源语义

回营恢复和事件恢复原因优先于泛化的 session-running 文案。View 只保留对象生命期和本地并发防御；无猎人由现有编队桌规则显示，不再静默返回。

## Risks / Trade-offs

- transient 不持久化；重新进入战役后由玩家再次点击触发，这是纯反馈的预期边界。
- 本 Change 不迁移 GameManager 剩余 Settlement 表现装配职责。
- 未新增独立 UI EventBus 事实，避免把显示行为误当成玩法事实。
