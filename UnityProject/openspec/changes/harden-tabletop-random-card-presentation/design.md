## Context

`TabletopRandomInteractionRequest`、路由和事件 Action 已支持 DrawCards、FlipCards 与 OldMaid，缺口位于生产 View：它直接生成 Cube，并由 `ClickProxy.OnMouseDown` 提交选择。这绕过了 Cards3D 的按下/松开与拖动阈值，也使卡牌自身无法表达正反面和可选状态。

## Goals / Non-Goals

**Goals:**

- 用一个窄的 Cards3D View 表达稳定牌身份、正反面和可选状态。
- 保持规则与后果在 ActionQueue，View 只返回物理选择结果。
- 取消或禁用时完整释放临时对象和输入所有权。
- 验证一条真实 Old Maid 营地事件生产闭环。

**Non-Goals:**

- 不新增牌组规则、数值、美术资源或动画。
- 不重构 GameManager、事件 Action、随机协议或骰子表现器。
- 不推进 Showdown。

## Decisions

- `TabletopRandomCard3D` 继承 `CardView3D`，持有交互期内的稳定 ID、索引、值、鬼牌标记和面状态；它只发出 Selected 回调，不判断成功失败。
- `TabletopCardInteractionPresenter` 继续拥有洗牌、布局、顺序选择、揭示和结果 DTO，并把可选性投影到实体卡牌。
- `CardView3D` 在所有卡牌上识别超过阈值的手势；只有 `EnableDrag` 为真时开始拖拽，但非拖拽卡也会取消该次点击，避免滑动误选。
- 表现器把外部取消与自身销毁绑定；`OnDisable` 取消当前请求，finally 释放根对象、运行时材质、背景输入租约和等待源。

## Risks / Trade-offs

- [修改共享指针基类影响其他卡牌] → 只改变“超过阈值后是否仍触发点击”，并回归验证现有猎人卡短按与拖放。
- [运行时材质泄漏] → 新卡牌保留基类实例材质所有权并在销毁时释放；模板材质只共享不销毁。
- [旧回调完成新请求] → 选择同时校验当前等待源、活动数组、索引与 View 实例；清理后旧卡不再具有活动身份。

## Migration Plan

无存档迁移。现有场景无需新增组件；组合根仍自动创建原表现器，运行时卡牌按请求临时生成。

## Open Questions

无。美术、翻牌动画和音效继续保留为后续表现扩展。
