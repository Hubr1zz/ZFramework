---
schemaVersion: 2
category: feature
title: 营地桌面猎人装备
---

# Tabletop Settlement Equipment Specification

## Purpose

定义玩家从 3D 猎人卡进入世界空间装备桌，并以实体卡在营地仓库和猎人装备槽之间转移装备；View 只提交意图，库存、装备实例、效果注入和持久化继续归属 Settlement ActionQueue。

## Requirements

### Requirement: Equipment begins from the selected hunter card

营地中的可用猎人 SHALL 以 3D 猎人卡展示，点击后 SHALL 打开该猎人的世界空间装备桌；装备桌 SHALL 同时展示营地装备仓库与九个猎人装备槽。

#### Scenario: A hunter is inspected

- **WHEN** 玩家点击营地中的猎人卡
- **THEN** 装备桌 SHALL 显示该猎人的属性、已装备实例和当前可用仓库装备卡

### Requirement: Equipment cards use physical drag intent

玩家 SHALL 能把仓库装备卡拖入猎人装备槽以请求装备，也 SHALL 能把已装备实例卡拖回仓库以请求卸下；非法落点 SHALL 保持权威状态不变并让卡牌返回原位。

#### Scenario: A stored equipment card is dropped into an equipment slot

- **WHEN** 目标槽可接收装备且没有同一卡牌命令正在等待
- **THEN** View SHALL 恢复卡牌视觉原位、锁定重复拖拽并提交一次装备命令

#### Scenario: An equipped instance is dropped into storage

- **WHEN** 玩家把已装备卡拖回仓库区域
- **THEN** View SHALL 提交该运行时装备实例的精确标识，而不是仅按物品名称卸下

### Requirement: Settlement ActionQueue owns authoritative equipment state

装备与卸装 SHALL 作为当前 Settlement runner 的根 Action 串行执行，并在执行时重验猎人归属、内容注册、仓库数量和装备限制；View SHALL NOT 直接修改仓库或猎人装备集合。

#### Scenario: Equipment commits

- **WHEN** 装备 Action 成功
- **THEN** 仓库数量、猎人装备实例与兼容名称列表 SHALL 原子更新，并在提交事实后刷新可见装备桌与持久化边界

#### Scenario: A reactor or competing request prevents equipment

- **WHEN** BeforeExecution Reactor 阻止 Action，或先执行的请求耗尽最后一件库存
- **THEN** 失败请求 SHALL 不改变权威状态，卡牌 SHALL 恢复为可再次拖拽并显示失败原因

### Requirement: Settlement context panels are mutually exclusive

装备、休养、成长、症状、招募、年鉴、发明确认、工坊建设和工坊制作 SHALL 共享营地桌面的单一上下文面板所有权；打开任一面板前 SHALL 关闭其余面板。

#### Scenario: Another settlement entry is activated while equipment is open

- **WHEN** 装备桌打开时玩家点击年鉴、招募或其他营地上下文入口
- **THEN** 装备桌 SHALL 先关闭，桌面 SHALL 只保留新打开的一个上下文面板

### Requirement: Equipment content remains configuration-driven

装备桌 SHALL 从已注册的物品内容和营地存储读取卡牌，不得在 View 中硬编码具体装备；未来读表适配 SHALL 能替换当前内容来源而不改变拖拽命令契约。

#### Scenario: A configured equipment item enters storage

- **WHEN** 已注册的非资源物品库存大于零
- **THEN** 装备桌 SHALL 在分页仓库中显示对应卡牌与实时数量
