## Context

生产流程已经以 `CampaignCalendarDefinition` 和 Settlement 回营 ActionQueue 作为日历权威，但旧 Spec 与遗留事件效果仍表达年度直推语义。3D 年鉴也只显示序号，没有消费活动战役绑定的季节名称。

## Goals / Non-Goals

**Goals:**

- 保持每次成功回营推进一个配置季节、末季才跨年的唯一权威路径。
- 在表、ScriptableObject 预检和运行时三处拒绝旧 `AdvanceYear` 内容效果。
- 让回营事实与 3D 年鉴使用冻结日历的季节身份和显示名。
- 修正恢复、迁移和构筑闭环中的旧年度契约。

**Non-Goals:**

- 不改变日历存档 schema、CalendarId 冻结策略或年度事件算法。
- 不增加季节效果或数值，不推进 Showdown，不改 GameManager 阶段 FSM。

## Decisions

- `EventEffectType.AdvanceYear = 13` 仅保留序列化兼容；所有内容入口 fail closed，GameCore 删除会返回成功的旧协议。这样不会产生第二个日历写入口。
- `HuntCompletedEvent` 在回营 root 提交时携带完成季和下一季的稳定 ID/显示名快照。通知只格式化事实，旧事实缺少快照时回退为序号。
- 年鉴从当前 `SettlementManager.Timeline.CurrentSeason` 获取只读季节定义；重绑 manager 时覆盖旧名称，不读取默认日历。
- 旧存档迁移仅按正式 `campaign-year-loop` 的保守规则解释，不通过迁移补年度 Timeline。

## Risks / Trade-offs

- [旧内容仍保存 AdvanceYear 数值] → 保留枚举槽位并在表、SO 和运行时都给出可诊断拒绝。
- [日历显示名后续修改] → 活动通知使用提交快照；年鉴使用该战役按 CalendarId 绑定的受支持定义。
- [多个旧 Spec 同时漂移] → 同一 Change 保存完整 MODIFIED Requirement，交由人类审查后统一 sync。
