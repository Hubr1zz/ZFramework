---
schemaVersion: 2
category: feature
title: 营地桌面工坊制作
---

## ADDED Requirements

### Requirement: Material discovery and crafting inventory are independent

`UnlockedByMaterial` 配方 SHALL 使用战役级已发现素材判断知识门禁，并继续使用当前库存判断能否制造。消耗最后一份素材 SHALL NOT 隐藏或重新锁定已知配方。

#### Scenario: The final discovered material is consumed

- **WHEN** 玩家消耗最后一份已发现素材完成制造
- **THEN** 配方 SHALL 继续显示，但再次制造 SHALL 因库存不足失败

#### Scenario: Campaign restore has no remaining material

- **WHEN** 存档中素材库存为零但其稳定 ID 已记录为发现
- **THEN** 继续战役后配方知识 SHALL 保留
