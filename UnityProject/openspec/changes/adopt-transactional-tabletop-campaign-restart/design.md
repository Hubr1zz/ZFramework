## Context

`TryDeleteAsync(...).Forget()` 与后续新战役保存没有先后保证。即使默认文件 Adapter 有版本门禁，删除任务仍可能在新保存之后获得文件锁并删除新快照。终局重启又是玩家请求的权威战役状态变化，应进入 Campaign runner，而不是由 View 回调直接改 Manager。

## Decisions

### Campaign runner owns the player restart command

`RestartCampaignAction` 在 Campaign ActionEnvironment 中串行执行。View 只提交命令并消费 `CampaignRestartResult`；Before Reactor 可阻止或注入 gameplay 前置流程，成功后发布 committed fact。

### Persistence precedes runtime publication

先创建 detached Settlement generation 并验证初始 ActionSession 所需内容，再等待删除完成、写入候选稳定快照，随后 CAS 替换 Settlement/Hunt generation 并将 FSM 归位到 Settlement。任一步失败都释放候选；删除已生效时尝试恢复上一份稳定载荷。

存档线性化、generation 替换、ActionSession 激活与补偿恢复集中在纯 Core `CampaignRestartTransaction`。`GameManager` 只保留正在结算的玩家流程门禁、稳定载荷采用和 3D 表现清理，避免 MonoBehaviour 继续拥有战役运行世代事务。

### Defeat presentation remains authoritative until success

终局 3D 卡在命令执行期间继续阻断背景 collider。失败时重新呈现原因并允许重试；只有 typed 结果成功后才释放输入并关闭。

## Boundaries

- ActionQueue 只承载重启这一 gameplay 命令，不承载卡牌开关、文字刷新或 collider 表现。
- 不调用 BossFight 胜利结算，不推进 Showdown 内容。
- 本阶段不把全部持久化职责迁入 ZFramework Module；独立 Core 事务通过现有 runtime lease 操作世代，后续可由 Module 直接拥有而不改变 View 或 Action 契约。
