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

GameCore SHALL 提供按稳定 ID 激活血脉的规则入口；具体触发事件、症状、特性或数值效果 SHALL 由后续表内容与 Action/Reactor 接入，不得硬编码在 3D View 中。

#### Scenario: 激活请求不匹配

- **WHEN** 请求的血脉 ID 与猎人持有身份不一致
- **THEN** 激活 SHALL 被拒绝且猎人状态保持不变
