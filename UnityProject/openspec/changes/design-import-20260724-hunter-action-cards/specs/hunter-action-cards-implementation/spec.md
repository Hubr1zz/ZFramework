---
schemaVersion: 2
category: feature
title: 猎人行动卡代码实现
---

# 猎人行动卡代码实现

## ADDED Requirements

### Requirement: 实现“猎人行动卡规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 行动卡由高内聚运行时模块管理
实现 SHALL 由行动卡模块持有卡面、可组合费用、效果、恢复进度和每回合可用状态，通过显式端口请求时点、战斗资源与棋盘操作。基础行动卡 SHALL 由初始配置创建普通行动卡实例；意志行动 SHALL 使用同一运行时管线。

#### Scenario: 解析卡牌配置
- **WHEN** 战斗组合根加载 CharacterActionCardData
- **THEN** 系统创建不修改 SO 的运行时定义与实例

#### Scenario: 结算卡牌
- **WHEN** 行动卡通过费用和目标校验
- **THEN** 模块按顺序产生费用提交、效果、时点和翻面结果，并等待每个异步步骤完成

#### Scenario: 映射基础卡与意志行动
- **WHEN** 组合根加载猎人初始卡和意志行动配置
- **THEN** 系统以相同运行时定义建立卡牌，并只通过费用与每回合可用策略表达意志行动差异

### Requirement: 费用由独立领域组件结算
实现 SHALL 将时点、战斗灵感、意志和特殊费用表示为可组合的纯 C# 费用定义，并通过准备与原子提交协议避免部分支付。

#### Scenario: 原子支付混合费用
- **WHEN** 一张行动卡同时包含资源费用和需要玩家选择的特殊费用
- **THEN** 系统在全部准备步骤成功后一次提交费用，任一步失败或取消均不保留部分支付

### Requirement: Action Queue 驱动动态结算
实现 SHALL 在行动卡 Feature 内提供确定性的纯 C# Action Queue，并由 Adapter 异步执行器驱动输入和表现；队列 SHALL 支持顺序执行、运行中追加、暂停、恢复、取消和失败短路。

#### Scenario: 等待输入后恢复队列
- **WHEN** 当前 Action 请求玩家输入
- **THEN** Adapter 等待输入完成，GameCore 保持可恢复队列状态，并从确定的下一步骤继续执行

#### Scenario: 运行中追加 Action
- **WHEN** 当前 Action 在结算时向队首或队尾追加步骤
- **THEN** 队列按追加位置执行新步骤，且权威结算不通过 fire-and-forget 越过未完成步骤
