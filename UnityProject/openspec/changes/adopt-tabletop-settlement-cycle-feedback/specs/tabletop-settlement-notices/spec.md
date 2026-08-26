## ADDED Requirements

### Requirement: Successful Hunt return produces one calendar-aware world-space notice

一次成功回营 SHALL 只由信息完整的 `HuntCompletedEvent` 生成一张世界空间归档卡。卡片 SHALL 直接格式化事实中的完成年/季与推进后年/季；同年推进 SHALL 表达新季节，跨年 SHALL 表达新年度。`SeasonAdvancedEvent` 与 `YearAdvancedEvent` SHALL NOT 为同一次回营生成额外 notice。

#### Scenario: The Hunt advances within the same year

- **WHEN** 回营把日历从第 1 年第 1 季推进到第 1 年第 2 季
- **THEN** 玩家 SHALL 看到一张“季节推进”归档卡
- **AND** 卡片 SHALL 同时显示完成前后的年/季坐标

#### Scenario: The Hunt advances into a new year

- **WHEN** 回营把日历从第 1 年末季推进到第 2 年第 1 季
- **THEN** 玩家 SHALL 看到一张“新年抵达”归档卡
- **AND** 不得因同次提交的其他时间事实再显示第二张回营卡

### Requirement: A departure block notice can interrupt and restore ordinary notices

出猎门禁反馈 SHALL 使用固定 transient key 更新同一张非阻塞卡。它 SHALL 立即覆盖正在显示的普通 notice；清除或超时后 SHALL 恢复被中断的 notice 及后续队列。重复相同或变化原因 SHALL NOT 无限追加卡片。Campaign reset SHALL 清除 active、pending 和 interrupted transient 状态。

#### Scenario: The player clicks departure while a return archive is visible

- **WHEN** 权威门禁拒绝出猎
- **THEN** 门禁原因 SHALL 立即成为当前 3D 卡
- **AND** 门禁解除后原回营归档卡 SHALL 恢复
- **AND** 普通 notice SHALL NOT 被删除或重复入队
