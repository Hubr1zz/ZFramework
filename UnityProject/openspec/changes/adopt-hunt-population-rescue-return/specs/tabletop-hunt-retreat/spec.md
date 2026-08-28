---
schemaVersion: 2
category: feature
title: 3D 狩猎回营结算
---

## MODIFIED Requirements

### Requirement: Retreat is a world-space card flow

正常 Hunt 回营 SHALL 使用持久的世界空间实体卡。状态桌与确认布局 SHALL 显示出发猎人、损失、通用携带物和同行幸存者；这些 View SHALL 只读取 Hunt 状态并提交稳定命令。

#### Scenario: Player inspects a rescue return

- **WHEN** 当前远征有一名同行幸存者并打开回营确认卡
- **THEN** 世界空间卡 SHALL 显示“同行幸存者 1”
- **AND** 打开、关闭或刷新布局 SHALL NOT 改变人口

### Requirement: Retreat location creates a visible cargo decision

普通安全回营和紧急撤退 SHALL 保留同行救援人口。远离营地时的弃置决策 SHALL 只从通用携带物中选择一个单位，不得把匿名人口作为物品卡或弃置目标；未来人口损失惩罚需使用独立配置化策略。

#### Scenario: The squad retreats away from camp with a survivor

- **WHEN** 小队远离营地并携带一名幸存者与一份物品
- **THEN** 玩家 SHALL 只选择是否放弃一份物品
- **AND** 成功准备的 v3 记录 SHALL 仍保存完整救援人口
