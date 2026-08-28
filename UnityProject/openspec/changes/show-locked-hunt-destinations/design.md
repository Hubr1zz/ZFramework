## Context

目的地目录已经用 `minimumYear` 与路线内容配置表达可用性，但 View 只读取当年可用项，导致未来路线从桌面消失。出发命令仍必须经过既有 Settlement/Campaign 权威事务，View 只负责展示与提交意图。

## Goals / Non-Goals

**Goals:**

- 用只读投影统一提供有效路线、当前可用性和权威原因。
- 让锁定路线作为不可交互的 3D 卡可见，并确保默认选择与确认只落在可用路线。
- 在失败重绘时按稳定路线 ID 保留玩家选择，并保留无可用路线时的 fallback。

**Non-Goals:**

- 不新增路线效果、数值或存档字段。
- 不改变配置化季节推进、GameManager、Showdown 或 ActionQueue。
- 不把浏览、选卡等 UI 行为包装为 GameAction。

## Decisions

1. 目录生成 `PlayableHuntDestinationAvailability` 只读投影，复用现有 `IsAvailable` 规则；无效配置不进入玩家列表。
2. 面板同时在展示、选择和确认三处检查 `IsAvailable`，防止锁定卡或陈旧回调提交。
3. View 按稳定路线 ID 恢复选择；目标失效时退回首个可用路线。
4. 当前年份没有任何可用显式路线时继续提交 `null`，由既有默认内容契约处理；不额外发明“默认路线卡”。

## Risks / Trade-offs

- 锁定路线会增加桌面卡片数量 → 继续复用现有自适应卡片布局，并只投影配置完整的路线。
- 面板打开期间年份或内容可能变化 → 失败后从当前年份重新投影，最终命令仍由权威事务重验。
- 未来解锁条件不只年份 → 投影保留通用 `Reason` 字段，不把年份判断写入 View。
