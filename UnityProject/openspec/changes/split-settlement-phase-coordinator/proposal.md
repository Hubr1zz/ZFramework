## Why

Settlement 的 ActionSession 与 3D 桌面回调仍由 GameManager 直接装配，阶段运行世代虽然已由 Campaign Runtime 持有，表现与会话仍可能在换代后接受旧回调。

## What Changes

- 新增由 `PlayableSettlementPhaseManager` 持有的纯 C# `PlayableSettlementPhaseCoordinator`。
- 将 Settlement ActionSession 的创建、幂等激活/停用和 SettlementTable3D 回调绑定收敛到 coordinator。
- 旧运行世代的桌面回调按当前 SettlementManager 身份 fail-closed；场景引用、出猎请求和回营事务仍由 GameManager 提供。

## Non-goals

- 不迁移回营两阶段保存、年度事件恢复、phase swap、场景根对象、持久化事务。
- 不改变 Hunt、Showdown、Calendar 或 ActionQueue 规则。
