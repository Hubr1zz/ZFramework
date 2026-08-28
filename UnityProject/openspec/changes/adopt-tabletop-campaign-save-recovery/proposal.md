## Why

普通营地提交、阶段进入与狩猎检查点使用后台保存；写入失败时当前实现只有日志，玩家无法判断最新进度是否可靠落盘，也没有安全重试入口。现有文件层已经防止旧写入覆盖新状态，本次补齐协调器可见状态与跨阶段 3D 恢复闭环。

## What Changes

- 战役持久化协调器投影 `Idle / Saving / Failed` 只读状态，并用请求 revision 与战役 generation 阻止旧完成覆盖新状态。
- 保存失败后保留可重试状态；重试重新捕获当前权威快照，并合并重复重试请求。
- 生命周期取消不伪装成磁盘失败，Reset/Adopt 会隔离旧战役迟到完成。
- 营地与狩猎桌面共享一张世界空间 3D 存档失败卡，玩家可点击重试，成功后自动收起。
- 关键回营、遭遇与重启事务继续等待原保存结果并保持既有失败门禁；存档 I/O 和提示不进入 ActionQueue。
- 修正正式 `campaign-persistence` 中已经过时的旧狩猎配额迁移描述，使其与配置化季节日历保持一致。

## Capabilities

### New Capabilities

- `tabletop-campaign-save-recovery`: 跨营地与狩猎展示存档失败，并允许基于最新权威状态安全重试。

### Modified Capabilities

- `campaign-persistence`: 明确旧配额字段只保守迁移为季节索引，不再把历史完成次数解释为额外跨年。

## Impact

影响 Campaign 持久化协调器、窄读写端口、全局桌面表现和对应定向测试。不改变存档 schema、文件封套、GameManager、阶段管理器、Showdown 或任何玩法 ActionQueue。
