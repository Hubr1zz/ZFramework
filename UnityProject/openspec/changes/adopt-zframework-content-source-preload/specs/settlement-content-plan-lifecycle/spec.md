---
schemaVersion: 2
category: architecture
title: "营地内容计划显式来源"
---

# 营地内容计划显式来源 Delta

## MODIFIED Requirements

### Requirement: Plan preparation is side-effect free

营地内容计划 SHALL 在离线准备阶段从同一显式内容源 Bundle 读取基础资产、扩展、物品表与配方表，并结合 Catalog 中显式引用的特性、猎人、发明与设施表创建候选对象和完成跨表校验。准备阶段不得扫描 Resources 路径，不得修改当前 Plan、兼容 Registry、Catalog 运行时字段或 SettlementManager。

#### Scenario: A candidate is interrupted after explicit-source preparation

- **WHEN** Bundle 中的 Item、Recipe、Extension 与 Catalog 表对象已经生成，但候选尚未发布
- **THEN** 当前 Plan 与三个兼容 Registry SHALL 保持安装前状态
- **AND** 被拒绝候选拥有的所有 transient Unity 对象 SHALL 被释放
- **AND** Manifest 持有的外部资产与事件世代 SHALL 保持有效
