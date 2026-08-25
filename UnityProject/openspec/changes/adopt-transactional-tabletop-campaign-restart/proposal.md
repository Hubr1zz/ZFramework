## Why

终局实体卡原先先关闭表现，再异步删除存档并直接替换营地。删除未被等待时可能晚于新快照写入，最终删掉刚建立的新战役；失败也没有可见反馈或重试入口，并绕过 Campaign ActionQueue。

## What Changes

- 将重写战役作为 typed Campaign ActionQueue 命令串行执行，允许 gameplay Reactor 阻止或注入前置流程。
- 先准备候选营地，再等待旧存档可靠删除并写入新战役稳定快照，最后以 generation CAS 发布运行态。
- 删除、保存、ActionSession 或阶段发布失败时释放候选、恢复旧稳定载荷并保留终局卡供重试。
- 终局卡只在权威重启成功后关闭；失败时继续冻结后台输入并显示原因。
- 不推进 Showdown 玩法，也不把 View 刷新或表现事件放入 ActionQueue。

## Capabilities

### Modified Capabilities

- `tabletop-game-over`: 终局卡从“先关闭再重置”改为等待 typed 重启结果，失败保持可重试。
- `campaign-persistence`: 重启删除、新快照写入和运行态替换形成有序、可回滚的持久化事务。

## Impact

- Campaign ActionSession 与 ZFramework Campaign Runtime 转发端口。
- GameManager 终局重启组合事务。
- 世界空间终局 View 的成功/失败交互。
- 不改变 Settlement/Hunt 内部玩法、UI 事件或 Showdown 规则。
