## Context

`DeathDeck` 已能无状态准备洗牌顺序并在稳定选位提交后解析牌面；`HunterManagementSystem` 已统一处理永久死亡、装备归还、年鉴、激励与名册事实。缺口是 Hunt 事件事务没有“准备规则 → 等待 3D 选牌 → 原子提交”的专用效果接缝。

## Goals / Non-Goals

**Goals:**

- 让一条正式 Hunt 事件对默认健康猎人稳定触发死亡牌堆。
- 保持规则、View、永久死亡后果和 occurrence 恢复各自单一权威。
- 玩家看到牌堆构成、不可区分卡背与翻开后的真实结果。
- 普通存活牌在父事件提交后排入一个可恢复的专属存活事件，并严格继承原猎人。
- 无 Presenter 的测试/无头环境继续走相同事务。

**Non-Goals:**

- 不改变确定死亡 `KillHunter`、Combat 死亡判定或 Showdown。
- 不实现特性、症状或其他效果动态注入的存活事件池，也不实现动画、音效或美术。
- 不持久化临时卡牌布局或未提交洗牌顺序。

## Decisions

- `EventFatalInjuryRules` 基于复制出的猎人伤势状态创建计划；表现前不写回猎人或死亡牌堆。
- 洗牌使用独立随机源，提交期效果随机与事件随机不因取消而前进；无头模式从已洗牌顺序的稳定位置 0 抽取。
- `FatalInjury` 必须独占成功或失败效果列表，避免其他效果先提交后致命伤失败。
- View 请求携带只读牌面标签，但只返回稳定选位；最终结果仍由 GameCore 计划解析。
- 死亡结果只调用 `IHunterDeathCommand`，最后猎人死亡沿既有战役失败与子链截断边界。
- `FatalInjury.survivalEventId` 必须解析到非自身 `Triggered` 事件；死亡牌为存活时才把该事件合入 child occurrence，父提交后子失败只恢复子。
- occurrence 含明确 actor ID 时只允许精确、存活的小队猎人；目标失效则保留 occurrence 并 fail closed，禁止静默改投。

## Risks / Trade-offs

- [进程在选牌前退出会重新洗牌] → occurrence 尚未提交，恢复后重新展示和洗牌符合未完成判定语义；不保存纯 View 状态。
- [内容作者把致命伤与奖励混排] → 表校验整条拒绝，避免运行时部分提交。
- [Presenter 缺失] → 已洗牌顺序位置 0 作为无头回退，保持同一规则与提交路径。
- [父提交后存活事件失败] → 子事件拥有独立 occurrence；恢复不重放死亡牌，明确 actor 失效时不把奖励转给其他猎人。

## Migration Plan

无存档结构迁移。新增枚举值和表字段只影响新内容；旧 `KillHunter` 与现有事件记录保持原语义。

## Open Questions

特性、症状与临时效果对存活事件池的动态注入，以及“展示正面后翻牌”的动画节奏留给后续扩展。
