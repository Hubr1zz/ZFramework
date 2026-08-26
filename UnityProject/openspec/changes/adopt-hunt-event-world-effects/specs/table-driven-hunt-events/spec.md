---
schemaVersion: 2
category: feature
title: 读表狩猎事件内容
---

## ADDED Requirements

### Requirement: Hunt world effects are bound to the committed event tile

读表 Hunt 事件 MAY 请求 world effect，但该能力 MUST 由当前 Hunt Action 提供，并且 SHALL 只作用于产生该事件的已提交地块。事件数据 SHALL NOT 指定任意坐标或资源点 ID；Settlement、其他事件类别及缺少 world command 的执行环境 MUST 失败关闭且不得修改地图。

#### Scenario: A quarry failure buries nearby resources

- **WHEN** 玩家在“呼吸的采石场”冒险判定失败
- **THEN** 石肺效果与 `ExhaustCurrentHuntTileResources` 按配置顺序结算
- **AND** 只有当前事件地块的未耗尽资源点被标记为耗尽
- **AND** 其他地块的资源点保持不变

#### Scenario: A world effect is submitted outside Hunt

- **WHEN** 非 Hunt 内容配置该效果，或执行环境没有当前地块 world command
- **THEN** 表校验或效果结果 SHALL 明确拒绝该请求
- **AND** 不得回退到 Settlement 规则或任意地图目标

### Requirement: Hunt world effects remain recoverable and observable

World effect SHALL 继续使用既有事件效果批次语义，报告解析目标、是否生效和受影响数量。重复作用于已耗尽地块 SHALL 成功且受影响数量为零；事件提交后的资源状态 SHALL 由活动狩猎检查点保存。恢复 pending occurrence 时 MUST 从已验证地图坐标重建执行上下文，而不是信任事件数据提供目标。

#### Scenario: A pending event resumes after a Reactor prevention

- **WHEN** occurrence 的首次执行被 Reactor 阻止并在同一活动狩猎中恢复
- **THEN** 恢复执行只修改 occurrence 所属的已验证地图地块
- **AND** 下一个检查点保存资源点的耗尽状态
