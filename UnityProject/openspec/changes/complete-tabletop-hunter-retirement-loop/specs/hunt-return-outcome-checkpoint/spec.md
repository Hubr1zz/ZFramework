---
schemaVersion: 2
category: feature
title: 狩猎归来结果检查点
---

## ADDED Requirements

### Requirement: Retirement archives equipment return exactly once

首次归来推进使存活参战猎人达到退休条件时，Settlement 提交 SHALL 把该猎人的全部有效装备稳定 ID 各归还仓库一次、清空其装备状态并写入唯一退休年鉴记录。只有首次归档 SHALL 发布包含猎人稳定 ID、显示名、年龄、提交年份和实际归还数量的 `HunterRetiredEvent`；重放同一归来或再次请求退休 SHALL NOT 重复库存、年鉴或事实。

#### Scenario: A hunter retires with equipment

- **WHEN** 有效归来首次把携带一件已注册装备的存活猎人推进到退休
- **THEN** 猎人 SHALL 变为退休且不可再次出发，装备 SHALL 出现在营地仓库
- **AND** 退休年鉴与可读退休事实 SHALL 各提交一次

#### Scenario: The retirement commit is replayed

- **WHEN** 同一归来或同一退休年鉴事件已经提交
- **THEN** 仓库数量、猎人装备、退休年鉴与退休事实 SHALL 保持不变
