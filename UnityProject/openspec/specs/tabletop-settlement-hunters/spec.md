---
schemaVersion: 2
category: feature
title: 营地桌面猎人卡交互
---

# Tabletop Settlement Hunters Specification

## Purpose

定义营地 3D 猎人卡的稳定桌游交互：点击用于查看猎人，拖拽用于调整兼容卡槽布局；权威状态刷新不得打断拖拽或无故重排玩家桌面。

## Requirements

### Requirement: Click and drag express different intent

猎人卡 SHALL 在指针松开且未形成拖拽时提交一次查看意图；超过拖拽阈值后 SHALL 只执行卡槽拖放，且 SHALL NOT 同时打开猎人档案或装备桌。

#### Scenario: A hunter card is clicked

- **WHEN** 玩家按下并松开猎人卡且没有开始拖拽
- **THEN** View SHALL 只提交一次该猎人的查看意图

#### Scenario: A hunter card is dragged

- **WHEN** 玩家移动指针超过拖拽阈值并结束拖放
- **THEN** 卡牌 SHALL 尝试进入兼容卡槽或返回原位，且 SHALL NOT 提交查看意图

### Requirement: Authoritative refresh preserves arranged hunter cards

猎人区 SHALL 以运行时猎人 InstanceId 增量同步卡牌。仍存在的猎人 SHALL 复用同一卡牌对象和当前兼容卡槽，只刷新显示数据；离开的猎人 SHALL 只释放自身卡槽，新出现的猎人 SHALL 使用首个兼容空槽，且 SHALL NOT 重排其他猎人卡。

#### Scenario: A squad arrangement is refreshed

- **WHEN** 已摆放的猎人仍存在，而另一猎人离开或新猎人加入
- **THEN** 保留猎人的卡牌对象及当前营地或编队槽 SHALL 不变，只有差异卡牌 SHALL 被移除或加入

### Requirement: Structural refresh waits for active hunter drag

猎人卡拖拽期间到达的结构刷新 SHALL 只缓存最后一份有效猎人快照，并 SHALL 在最后一个活动拖拽结束后应用。

#### Scenario: Roster changes during drag

- **WHEN** 拖拽期间连续收到多次猎人列表刷新
- **THEN** 当前拖拽 SHALL 正常结束，随后猎人区 SHALL 一次投影最新快照

### Requirement: Hunter arrangement remains presentation state

猎人卡的位置和手势 SHALL 只属于当前 View 会话，不写入战役存档，不创建 GameAction，也不通过 ActionQueue 发布 UI 事件；猎人加入、离开和属性变化仍由权威玩法系统提交。

#### Scenario: A player rearranges hunter cards

- **WHEN** 玩家只在兼容猎人卡槽之间拖动卡牌
- **THEN** 猎人权威数据、战役存档与 ActionQueue SHALL 保持不变
