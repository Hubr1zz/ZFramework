## Context

三阶段 manager 已由 ZFramework CampaignRuntime 唯一拥有，但旧 `IPlayableCampaignPhaseManagerAccess` 直接返回具体实现。GameManager 还从 coordinator 取得 manager/session factory，再回传 CampaignRuntime；Hunt coordinator 的 current provider 也由 GameManager 注入。所有权在对象图上成立，在 API 权限上仍未封闭。

## Goals / Non-Goals

**Goals:**

- 让 GameManager 无法取得具体 phase manager/coordinator。
- 让 Settlement/Hunt 的构造 factory 与 current-generation provider 只存在于对应阶段管理器内部。
- 保持现有跨阶段事务、恢复回滚、释放顺序和公共玩法兼容 API。

**Non-Goals:**

- 不继续拆分跨阶段事务或 GameManager 的公共兼容门面。
- 不改变 ActionQueue、存档、Showdown 玩法或 3D 表现设计。

## Decisions

- 定义一个组合根访问契约和三个 internal phase ports；CampaignRuntime 仍是具体 manager 的唯一 owner。
- Settlement/Hunt phase manager 自行创建 `Playable*RuntimeConfiguration`，端口不返回 `HuntManager` 或 `Playable*ActionSession` factory。
- Hunt manager 固定以自身 current generation 配置 coordinator，调用方不能注入 current provider。
- 删除 CampaignRuntime 公共接口上的任意 runtime factory 配置入口；测试故障注入仅通过反射 internal manager，不形成生产 API。
- Showdown 继续允许 GameManager 通过端口实现既有兼容读写门面，本阶段不扩大战斗重构。

## Risks / Trade-offs

- [端口演变成 coordinator 的全量复制] → 只保留 GameManager 当前需要的阶段操作，不公开具体 coordinator 或构造 factory。
- [测试失去故障注入] → 测试程序集通过反射调用 internal 配置，不为测试保留生产逃逸口。
- [收口误改事务时序] → 不搬迁出发、撤退、恢复或遭遇事务，并用既有 campaign-loop PlayMode 回归验证。
