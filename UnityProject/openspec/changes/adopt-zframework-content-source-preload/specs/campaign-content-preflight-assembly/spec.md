---
schemaVersion: 2
category: architecture
title: "战役内容源预检装配"
---

# 战役内容源预检装配 Delta

## ADDED Requirements

### Requirement: Campaign content uses one explicit source bundle

正式 Bootstrap SHALL 从已准备的 `PlayableContentSourceBundle` 构建候选。Bundle SHALL 由稳定 Manifest 聚合 Settings、事件/血脉/物品/配方表、营地扩展与字体；正式装配 SHALL NOT 使用 `Resources.Load`、`Resources.LoadAll` 或路径扫描补齐缺失内容。

#### Scenario: A required source is absent

- **WHEN** Manifest 缺少必需表、字体或启动配置
- **THEN** Bundle 创建 SHALL 返回结构化诊断并失败
- **AND** Campaign Candidate 与 GameManager SHALL NOT 被创建

## MODIFIED Requirements

### Requirement: Build is side-effect free and fail-closed

候选构建 SHALL 只读取显式内容源 Bundle 和其中的启动配置并生成结构化诊断，不得修改已安装 Runtime。缺失 Bundle、无效血脉表、缺失必需目录、重复目的地稳定 ID或最早可用年份无狩猎内容时 SHALL 拒绝候选。

#### Scenario: Invalid content bundle is submitted

- **WHEN** 内容源 Bundle 为空、血脉表无效或 Settings 包含无效目的地
- **THEN** 构建 SHALL 返回错误诊断
- **AND** 当前 Runtime 引用 SHALL 保持不变
