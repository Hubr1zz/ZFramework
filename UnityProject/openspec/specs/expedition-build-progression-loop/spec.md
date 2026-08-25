---
schemaVersion: 2
category: feature
title: 远征奖励与营地构筑闭环
---

# Expedition Build Progression Loop Specification

## Purpose

定义非决战路径中“狩猎获得资源、撤退回营、制造装备、装备猎人、存档恢复”的最小可玩构筑闭环，并确保所有游戏性写入由阶段 ActionQueue 执行。

## Requirements

### Requirement: Hunt rewards cross the return boundary exactly once

狩猎事件或采集获得的资源 SHALL 先进入本次远征携带物；成功撤退后，Settlement 回营 Action SHALL 将其一次性提交到营地库存、记录远征并推进一年。制造或装备操作不得在有效回营检查点清除前开始。

#### Scenario: Black salt returns from an expedition

- **WHEN** 猎人在狩猎中获得一份 `black_salt` 并成功撤退
- **THEN** 资源 SHALL 从远征携带物转入营地库存，回营记录 SHALL 被应用且当前年份 SHALL 恰好增加一

### Requirement: Crafting and equipment use authoritative settlement actions

制造与装备 SHALL 使用当前内容计划中的配方和稳定物品 `ContentId`，并分别作为 Settlement runner 的根 Action 串行执行；View 和兼容 UI SHALL NOT 直接修改资源、仓库或猎人装备集合。

#### Scenario: A returned resource becomes an equipped item

- **WHEN** 玩家使用一份 `black_salt` 制作 `salt_ward`，随后把该装备交给本次远征猎人
- **THEN** 原料 SHALL 被扣除，装备 SHALL 从营地仓库转入猎人装备实例与稳定 ID 列表，且每一步只提交一次对应事务事实

### Requirement: The build survives save and restore

营地事务提交后 SHALL 冻结包含稳定装备 ID 的战役存档；继续战役 SHALL 从同一内容计划恢复运行时装备实例，不得恢复已应用的有效回营检查点。

#### Scenario: Continue after equipping a salt ward

- **WHEN** 玩家在装备 `salt_ward` 后保存并继续战役
- **THEN** 恢复的猎人 SHALL 拥有一个对应运行时装备实例，并 SHALL 能通过 Settlement ActionQueue 卸下该实例并返还仓库

### Requirement: The restored build changes the next expedition

表驱动装备属性 SHALL 参与下一次狩猎的权威玩法结算，而不只是存档或展示数据。狩猎风险 SHALL 只读取本次已出发且仍存活猎人的运行时装备，并在普通地块提交前由 Hunt ActionQueue 冻结。

#### Scenario: A restored salt ward protects the next expedition

- **WHEN** 装备 `salt_ward` 的单人队伍在继续战役后再次出猎
- **THEN** 护符的负噪音修正 SHALL 抵消该猎人的一点基础噪音，最终风险 SHALL 不低于零，且未出发或死亡猎人的装备 SHALL NOT 参与结算

### Requirement: Legacy screen-space settlement UI is not authoritative

正式营地交互 SHALL 以 3D 桌面入口为准；遗留屏幕空间详情若仍存在 SHALL 只读展示，不得持有可绕过 Settlement ActionQueue 的游戏性写接口。

#### Scenario: A legacy hunter detail panel is opened

- **WHEN** 兼容入口显示猎人装备信息
- **THEN** 它 SHALL NOT 直接装备、卸装、制造、解锁发明或结算事件
