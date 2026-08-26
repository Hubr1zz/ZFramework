---
schemaVersion: 2
category: feature
title: "读表猎人症状内容"
---

# Table-driven Hunter Symptoms Specification

## Purpose

让可扩充的症状内容以稳定 ID、数值惩罚、内化收益与克服条件进入既有猎人规则、事件和 3D 桌面流程，而不把内容硬编码进 View 或 Action。

## Requirements

### Requirement: Symptom content is versioned and table-driven

正式症状目录 SHALL 引用一个版本化 JSON 表作为唯一内容源；每条记录 SHALL 提供唯一稳定 ID、玩家显示名、说明、负面修正、内化收益与克服参数。

#### Scenario: Campaign content is assembled

- **WHEN** 营地内容候选执行预检
- **THEN** Adapter SHALL 从表构建只读症状定义，并向既有 GameCore 症状规则提供无 Unity 资产依赖的值

### Requirement: Invalid symptom tables fail closed

表解析 SHALL 拒绝未知版本、空内容、无效稳定 ID、重复 ID、重复显示名、冲突旧别名以及负数消耗；正式目录 SHALL NOT 在已配置表失效时静默使用陈旧内嵌内容。

#### Scenario: Two symptoms share a display name

- **WHEN** 症状表被加载
- **THEN** 内容装配 SHALL 失败且不得发布部分症状目录

### Requirement: Persistent symptom identity remains stable

事件、猎人状态和存档 SHALL 使用症状稳定 ID；已登记的旧显示名或旧别名 MAY 在同步时解析为同一稳定 ID，修改玩家显示名不得改变已保存症状身份。

#### Scenario: A legacy hunter stores an old symptom alias

- **WHEN** 猎人进入营地内容投影
- **THEN** 该引用 SHALL 解析为表中稳定 ID，负面修正只应用一次

### Requirement: Derived symptom traits are preflight validated

每项症状对应的内化与克服特性 SHALL 在特性表中以稳定 ID 登记；缺少任一派生特性时，战役内容候选 SHALL 在发布前失败。

#### Scenario: A new symptom omits its overcome trait

- **WHEN** Campaign 内容候选校验跨表引用
- **THEN** 候选 SHALL 被拒绝且当前已发布运行时保持不变

### Requirement: Existing gameplay and tabletop flows are reused

症状获得、面对、内化和克服 SHALL 继续由既有 GameCore 规则与 Settlement ActionQueue root 提交；3D 症状卡 SHALL 只读取目录并转发玩家命令，不得自行修改猎人状态或向 ActionQueue 发送纯 UI 事件。

#### Scenario: Player internalizes a table-defined symptom

- **WHEN** 3D 症状面板提交有效选择
- **THEN** Settlement Runner SHALL 提交意志、进度与派生特性变化，并由 View 只读刷新结果

### Requirement: Production events exercise configured symptoms

基础营地与狩猎随机池 SHALL 以稳定 ID 提供获得表中症状的可玩失败路径；每个此类事件 SHALL 同时提供至少一个不强迫玩家承担症状风险的保底选项，并 MAY 通过装备或特性关键词提供更优解。

#### Scenario: A hunter fails a tabletop event check

- **WHEN** 物理骰子、翻牌或抽鬼牌结果未通过事件判定
- **THEN** 当前阶段 Runner SHALL 在同一事件 root 中幂等登记配置症状，并由既有 3D 结果确认流程展示变化
