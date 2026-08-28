## Context

Settlement 与 Hunt runner 已经把事件节点作为 ActionQueue 根因果链执行，但早期 `EventSystem` 仍同时承担效果解析和全局队列编排。旧 Settlement HUD 与 Hunt uGUI fallback 虽然已无 scene/prefab/asset GUID 引用，仍在代码组合边界保留入口。

## Goals / Non-Goals

**Goals:**

- 使 ActionQueue 成为事件链与玩法提交的唯一流程权威。
- 保留已经验证的事件效果、重投、物品、人口、致命伤与遇敌解析能力。
- 生产 Settlement/Hunt 表现只组装世界空间 3D 入口。

**Non-Goals:**

- 不重命名 `EventSystem`，不重写事件规则或 ActionQueue。
- 不调整 EventBus after-commit 通知、存档 schema 或 Showdown/Combat 玩法；战斗存活事件保持既有兼容边界。
- 不对开发者工具 UI 做同类清理。

## Decisions

1. `EventSystem` 保留为纯 C# 效果 resolver adapter。删除共享 queue、selected actor、UI callbacks 和继续队列 API；保留显式 actor/context 的 narrative/choice/effect 计算。整体替换 resolver 会重写已验证的效果边界，收益不足以覆盖风险。
2. Settlement phase port 不再接收 `SettlementUIManager`。猎人点击由 `SettlementTable3D` 内部装备面板处理，出发依然经 3D 编队/目的地入口提交。
3. `HuntUIManager` 保留为阶段生命周期内的 3D presenter owner，不整体删除。它只管理 `HuntStatusBoard3D` 与 `HuntHarvestPanel3D`，并支持从无表现绑定升级为正式 3D 绑定。
4. 删除脚本前同时审计 C# 动态创建和 Unity GUID 序列化引用；已删除脚本均在 `.unity/.prefab/.asset` 中零命中。

## Risks / Trade-offs

- [无 3D Hunt 根的旧调试场景不再获得屏幕采集 UI] → 正式 Hunt 启动本就要求地图、状态板和会话原子就绪；无根入口保持不可玩而非静默降级。
- [删除 MonoBehaviour 可造成 Missing Script] → 删除前复核所有相关 GUID，并使用 Unity 6000.5.9f1 重新导入和编译。
- [已有外部代码调用旧 public queue API] → 这是有意的 breaking cleanup；项目生产源码零调用，测试改为验证显式 transaction。

## Migration Plan

1. 去除 resolver 共享状态与队列 API，把测试改为显式 transaction。
2. 从 Settlement composition port 删除 HUD 参数，删除零 GUID 引用的 Settlement HUD 脚本。
3. 收窄 Hunt presenter 并删除零 GUID 引用的 screen-space fallback。
4. 编译，定向验证事件 transaction、3D 事件、战役循环与 3D 采集。

## Open Questions

- `EventSystem` 后续可在不改变契约的独立重构中更名为 `PlayableEventResolver`；本次不为纯命名产生大量构造点 churn。
