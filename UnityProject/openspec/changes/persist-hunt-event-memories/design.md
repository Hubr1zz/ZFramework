## Context

Hunt occurrence 已能在 ActionQueue 提交边界区分父、子和重复兄弟，但已提交结果只存在于短期运行态。活动狩猎存档只保存 pending/committed occurrence，回营记录也未携带选择、判定和致命伤事实，导致回营后 3D 年鉴无法还原玩家经历。实现必须兼容 v2/v3 活动狩猎和 v0-v3 回营记录，并保持当前遭遇生命周期边界。

## Goals / Non-Goals

**Goals:**

- 在同一 Resolution checkpoint 原子记录 occurrence 与结构化结果记忆。
- 让父已提交、子 pending 的存档只恢复子节点，不重放父效果或死亡牌。
- 通过活动狩猎 v4 与回营 v4 将有界、可验证的记忆传递到营地历史。
- 在 3D 年鉴中保持“远征摘要—事件子条目”的发生顺序。

**Non-Goals:**

- 不改变 Combat、Showdown 或遭遇交接后的 Hunt session 生命周期。
- 不增加事件数值、批量内容、动画、音效或新 UI Action。
- 不把年鉴读取交互纳入 ActionQueue。

## Decisions

1. `EventResolutionMemory` 与 `EventResolutionMemoryEffect` 是 Settlement/Hunt 共用的唯一 DTO，旧 Settlement 类型只保留源码兼容别名。统一规则负责验证、深拷贝和等价比较，避免各阶段复制逻辑漂移。
2. Hunt memory 以 `expeditionId + occurrenceSequence + eventId` 构成 `MemoryId`，并显式保存 SourceContextId 与 OccurrenceSequence。恢复时要求序号已提交、每序号最多一条、身份精确匹配。
3. 只有 Resolution checkpoint 写 memory；prevent、cancel、reroll-only 不写。提交后表现失败保留已提交 memory，后续恢复从 pending child 继续。
4. ActiveHunt 与 HuntReturn 协议分别升级为 v4。旧版本只读取其原有字段且不得夹带 memory；未来版本、超限集合、过长字段、跨远征身份和伪造序号均 fail closed。
5. 生产回营记录复用活动运行态的 ExpeditionId 作为 RecordId，使存档、回营幂等键和 memory 上下文保持同一身份。没有 ExpeditionId 的兼容测试入口仍可结算无 memory 的旧式记录。
6. 年鉴 View 使用非展示分组/顺序键排序，不依赖标题；FatalInjury 只投影玩家可读结果，不显示牌堆 ID 或牌位技术字段。

## Risks / Trade-offs

- [v4 载荷增大] → 每次远征最多 256 条记忆、每条最多 64 个效果，并限制 ID 与文本长度。
- [旧存档缺少新字段] → v2/v3 明确按无 memory 迁移；不伪造历史。
- [通用 DTO 替换旧 Settlement 列表类型] → 保持 JSON 字段名不变并保留旧类型别名，通过 JsonUtility round-trip 验证。
- [年鉴条目增多] → 复用既有分页 3D 面板；本阶段不增加筛选和美术演出。

## Migration Plan

新写入统一使用 v4。读取 v2/v3 ActiveHunt 和 v0-v3 HuntRecord 时保留既有字段，memory 必须为空；v4 要求显式子 schema 与完整身份校验。若回滚到旧版本，旧运行时会忽略新增 JSON 字段，但不得继续保存已进入 v4 的活动狩猎。

## Open Questions

无。跨遭遇续接 Hunt session 仍由独立设计处理。
