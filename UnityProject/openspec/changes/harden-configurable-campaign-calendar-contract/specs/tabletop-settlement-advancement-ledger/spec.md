---
schemaVersion: 2
category: feature
title: 营地桌面成长训练与年鉴
---

## MODIFIED Requirements

### Requirement: 年鉴是可随时打开的桌面物件

营地 SHALL 提供年鉴入口卡，显示当前年份、活动战役绑定日历中的当前季节显示名、最近一次远征与记录数；年鉴板 SHALL 把时间线和狩猎历史合并为按年份排序的分页实体条目。

已掌握的发明 SHALL 作为已完成的长期记录进入年鉴；狩猎历史中持久化的物品 ContentId SHALL 在 View 边界解析为玩家可读名称，不得直接显示技术标识。年鉴重绑另一个 Settlement runtime 时 SHALL 清除旧季节名称并使用新 runtime 的冻结日历。

#### Scenario: 记录超过单页容量

- **WHEN** 时间线与狩猎历史合计超过八条
- **THEN** 年鉴 SHALL 分页且任何条目不得超出面板边界

#### Scenario: 存在未来事件

- **WHEN** 时间线条目尚未完成
- **THEN** 年鉴 SHALL 以不同状态显示“将发生”，保留玩家对未来战役的预期

#### Scenario: 狩猎回营推进季节

- **WHEN** 稳定狩猎记录已经提交并使营地进入同年下一季或下一年首季
- **THEN** 年鉴入口卡和已打开的年鉴 SHALL 立即显示活动战役配置的当前季节名称与新的总记录数，不得显示年度出猎配额

#### Scenario: 发明或狩猎资源写入年鉴

- **WHEN** 发明提交成功，或狩猎记录包含已注册物品的稳定 ContentId
- **THEN** 年鉴 SHALL 显示一条去重的“发明 · 已掌握”记录，并以物品显示名汇总狩猎收获
