---
schemaVersion: 2
category: feature
title: "读表猎人血脉"
---

# Table-driven Hunter Bloodlines Specification

## Purpose

让每名猎人在进入战役名册时获得一次可持久化的随机血脉，并为后续事件激活、症状、特性与成长内容提供稳定扩展身份。

## Requirements

### Requirement: Bloodline content is table-driven

血脉内容 SHALL 从可替换的数据表读取稳定 ID、显示名、叙事说明、激活提示与正整数抽取权重；无效或重复条目 SHALL NOT 进入可抽取内容。

#### Scenario: 血脉表有效

- **WHEN** 营地内容初始化
- **THEN** Adapter SHALL 向 GameCore 提供无 Unity 对象依赖的只读血脉定义

### Requirement: A hunter receives one authoritative bloodline

新猎人 SHALL 在招募 Action 提交前按配置权重抽取一次血脉；稳定血脉 ID、缓存显示名与激活状态 SHALL 存入猎人权威数据。View SHALL NOT 自行抽取或修改血脉。

#### Scenario: 招募成功

- **WHEN** Settlement Runner 提交一名新猎人
- **THEN** 该猎人 SHALL 在进入名册与发布招募事实前拥有有效血脉，且 3D 招募反馈与猎人详情可读取它

#### Scenario: 招募被阻止

- **WHEN** Reactor、资源或名册校验阻止招募
- **THEN** 营地名册、资源与持久血脉状态 SHALL 全部保持不变

### Requirement: Save restoration never rerolls known bloodlines

加载已有血脉稳定 ID 时，系统 SHALL 只根据当前表刷新缓存显示名并保留激活状态；旧存档中缺少血脉的猎人 MAY 在迁移同步时获得一次血脉。

#### Scenario: 已有猎人恢复存档

- **WHEN** 猎人已经保存了可识别的血脉 ID
- **THEN** 同步 SHALL 保留该 ID 和激活状态，不受当前随机源结果影响

### Requirement: Activation remains an explicit extension boundary

GameCore SHALL 提供按稳定 ID 激活血脉的规则入口；具体触发事件、症状、特性或数值效果 SHALL 由表内容与 Action/Reactor 接入，不得硬编码在 3D View 中。

#### Scenario: 激活请求不匹配

- **WHEN** 请求的血脉 ID 与猎人持有身份不一致
- **THEN** 激活 SHALL 被拒绝且猎人状态保持不变

### Requirement: Events can gate and activate bloodlines

事件表 SHALL 支持按稳定血脉 ID 判断“持有”与“已激活”状态，并支持 `ActivateBloodline` 效果。条件显示 SHALL 使用玩家可读血脉名，不得暴露技术 ID。

#### Scenario: 匹配的未激活猎人面对血中旧梦

- **WHEN** 玩家为该猎人选择对应的血脉选项
- **THEN** 事件节点 SHALL 在 Settlement ActionQueue 根中激活血脉、授予配置特性并发布一次事件事务提交

#### Scenario: 猎人血脉不匹配或已经激活

- **WHEN** 3D 事件卡评估血脉选项
- **THEN** 该选项 SHALL 禁用并显示玩家可读原因，且绕过 View 直接提交也不得改变猎人状态

### Requirement: Bloodline event content is independently extensible

血脉事件 SHALL 位于独立事件表并合并进统一事件内容目录；新增血脉事件不得要求修改既有基础事件记录。

#### Scenario: 营地进入第二年后的随机事件抽取

- **WHEN** “血中旧梦”满足年度与随机池规则
- **THEN** 它 SHALL 复用现有实体事件卡、猎人选择和结果确认流程
