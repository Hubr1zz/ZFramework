## Context

3D Settlement 桌面已经拥有出发、成长、症状和通知的正式入口。旧 compatibility port、GameManager facade 与 IMGUI 组件没有生产序列化引用或正常调用者，但仍扩大了组合根契约和可达状态空间。

## Goals / Non-Goals

**Goals:**

- 使 Settlement 出猎唯一经过 3D 编队/目的地输入和 Campaign typed transaction。
- 使成长、症状和长期通知只保留 3D View → Settlement runner → after-commit fact 路径。
- 收窄 phase/runtime 构造参数，删除无引用旧脚本，并让测试验证旁路不存在。

**Non-Goals:**

- 不改 GameCore 规则、存档 schema、ActionQueue、Combat 或 Showdown。
- 不删除 `ApplyAfterHunt` 兼容重载或 `HunterGrowthSpentEvent`。
- 不改正式 3D destination View、input registration、`DepartForHuntAsync` 或 pending-return 门禁。

## Decisions

1. 删除 `ISettlementDepartureRequestPort`、`SettlementManager.TryDepart` 及其 runtime/phase 注入；保留 `IPlayableHuntDepartureInput` 作为 3D View 的窄输入端口。
2. 删除 GameManager 的 `SpendHunterGrowthAsync` 与旧 departure public methods；Settlement gameplay port 和 typed departure command 保持不变。
3. 删除无 GUID 序列化引用的 `PlayableSymptomGrowthService/View` 与三个 IMGUI Toast；不创建替代 UI，因为现有 3D 面板和通知 presenter 已承担正式职责。
4. 测试通过反射和正式 table callback 验证旧公开旁路关闭、pending-return 通知去重及恢复后 typed departure 成功。

## Risks / Trade-offs

- 旧外部脚本若依赖已删除 public API 将需要迁移；项目生产源码和序列化资源已静态核验为零调用/零 GUID 引用。
- 当前生成的 Unity C# project 文件可能暂时保留已删除脚本条目，需由 Unity reimport 重新生成；不手改生成文件。
- PlayMode 证据受 Unity license handshake 阻塞，需在可用许可证环境补跑。
