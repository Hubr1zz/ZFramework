## Context

项目已有表驱动事件链、Campaign/Settlement/Hunt 三类 ActionEnvironment、跨环境 installer registry、噪音风险牌组和营地权威保存边界。缺口不是新的效果 DSL，而是把持久营地事实安全投影到当前及未来匹配环境的桥接器。

## Decisions

### 租约是持久事实，Reactor 是运行时投影

`SettlementInstance.PendingHuntNoiseLease` 保存稳定 LeaseId、SourceEventId、schema 与修正值。Campaign-owned projection 校验该事实并注册 immutable Hunt-only installer；每个 Hunt environment 再拥有自己的 Reactor registration。阶段 session、View 和 `PlayableHuntRuntime` 均不知道具体租约。

### 选项事务先预检再提交

只有 Settlement 的 Random、Scheduled、Triggered 事件选项允许创建租约。即时效果和 Hunt 事件拒绝该类型。选项批次在资源收益前预检租约；相同租约重放幂等，单槽冲突或非法值整批失败，避免半提交收益。

### 生命周期由 Campaign registry 管理

投影同步先注册候选 installer，成功后再释放旧 registration；authority swap 失败恢复旧投影。Hunt session 释放只移除该环境 Reactor，不消费持久租约，因此失败出发和 session 重建可重试。Campaign Reset/Dispose 清理所有 registration。

### 成功回营是唯一消费边界

回营 Action 在资源、成长和日历变更前清除租约与 installer。验证失败、Reactor prevent 或取消均保留租约；已应用记录恢复路径也幂等清理。下一阶段会把现有年度写死语义改为配置化季节提交，本 Change 不定义年份推进规则。

## Risks / Trade-offs

- 当前只允许一个 pending 风险租约，避免在没有叠加顺序设计时发明通用效果栈。
- 生产案例使用固定 `+2`，仅验证流程；后续平衡仍由表配置调整。
- `GameManager` 仍在 Unity 组合根构造投影工厂；随阶段管理器拆分迁到战役装配职责，不在本阶段扩建注册框架。
