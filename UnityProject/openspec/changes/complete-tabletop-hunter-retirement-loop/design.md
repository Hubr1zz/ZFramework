## Context

`HunterAdvancementRules` 已负责退休判定，`SettlementHuntReturnAction` 也已在归来 root 内调用推进适配；缺口位于提交结果的可观察性。旧接口只执行归还并发布名册刷新，无法告诉表现层退休者姓名、年份和实际归还数量，也无法区分首次提交与幂等重入。

## Goals / Non-Goals

**Goals:**

- 保持退休判定、状态修改和装备归还由既有玩法系统掌权。
- 只为首次成功归档发布一条稳定、可读的退休事实。
- 通过既有 3D 名册、仓库卡区和招募入口形成长期战役闭环。
- 验证保存后继续战役不会重复库存、年鉴或通知。

**Non-Goals:**

- 不新增退休数值、传承效果、动画、音效或批量内容。
- 不重构 GameManager、阶段管理器或猎人数据模型。
- 不推进 Showdown。

## Decisions

- `HunterManagementSystem.TryCompleteRetirement` 以年鉴事件 ID 作为首次归档门禁，并返回实际写入仓库的装备数量；旧 `CompleteRetirement` 保留为兼容入口。
- `HunterRetiredEvent` 是已提交玩法事实，包含稳定猎人 ID、显示名、年龄、年份和归还数量；它通过既有 EventBus outbox 发布，不作为 UI Action 进入 ActionQueue。
- `SettlementNoticePresenter3D` 只排队投影退休事实，不修改玩法状态。名册与仓库继续由既有刷新事件读取权威数据。
- 读档只恢复权威快照，不重放已经提交的退休事实；玩家仍可使用既有招募 Action 补员。

## Risks / Trade-offs

- [旧调用方仍使用 void 接口] → 保留兼容包装，由新适配路径使用可判定首次提交的接口。
- [装备旧显示名与稳定 ID 并存] → 复用既有内容 ID 解析和仓库存储入口，不在退休流程另建迁移规则。
- [退休与归来摘要同时发布] → 3D 通知队列按提交顺序保留两条记录，退休档案不会被摘要覆盖。

## Migration Plan

无存档结构变更。既有退休猎人按保存状态继续恢复；只有今后的首次退休提交会产生新增可读事实。

## Open Questions

退休传承、纪念物和后代加成继续留给后续配置化内容设计。
