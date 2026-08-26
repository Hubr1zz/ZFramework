## Scope

`PlayableSettlementPhaseManager` 创建并唯一持有 coordinator。Settlement generation 仍持有 Manager/Data 与公开兼容接口，但 ActionSession 的实际实例只由 coordinator 保存；runtime 通过 owner generation 转发查询与生命周期。

`SettlementTable3D` 的场景引用或运行时 fallback 由 GameManager 在 Awake 配置给 coordinator。coordinator 每次激活当前 generation 时幂等重绑所有阶段内命令回调，回调执行前验证当前 generation 与 SettlementManager 身份，过期 View 不写入新运行态。

coordinator 同时负责 2D 营地 UI 与 3D 桌面的幂等 Init/Refresh；GameManager 只注入这些场景引用，并保留阶段根物体、跨阶段出猎入口、回营保存与恢复编排。其既有兼容属性继续从当前 coordinator/runtime 读取。
