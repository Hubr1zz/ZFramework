---
schemaVersion: 2
category: architecture
title: 营地内容计划生命周期
---

## ADDED Requirements

### Requirement: Workshop catalog belongs to the published settlement generation

Campaign 内容安装 SHALL 在发布完成前验证工坊 ID 规范且唯一，成本物品与前置发明属于当前 Settlement Plan，并且每个非空配方工坊 ID 均能解析到同一目录。任一错误 SHALL 拒绝候选。

#### Scenario: A recipe references a missing workshop

- **WHEN** 配方声明了当前工坊目录不存在的稳定工坊 ID
- **THEN** Campaign 安装 SHALL 返回明确诊断并失败

### Requirement: Material discovery schema migrates before gameplay

旧存档 SHALL 将当前正库存素材幂等补种为已发现知识；知识 ID SHALL 规范化、去重并稳定排序。未来版本 schema SHALL 在修改 Settlement 之前失败关闭。

#### Scenario: A legacy save contains black salt

- **WHEN** 旧存档有正数 `black_salt` 库存且尚无素材发现 schema
- **THEN** 内容投影 SHALL 补种其稳定 ID，后续耗尽库存也不得移除
