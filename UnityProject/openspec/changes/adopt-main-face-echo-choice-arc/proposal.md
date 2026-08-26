## Why

首个“巨石人脸”主线会在两年后安排 `main_face_echo`，但原内容只自动发放资源，没有新的玩家决策。现有时间线、3D 事件卡、实体骰子、Settlement ActionQueue、普通伤势与存档恢复已经具备完整能力，因此下一步应补一条可验证的生产内容弧，而不是新增事件系统。

## What Changes

- 保留 `main_face_echo` 稳定 ID、两年日程与既有子链，把它升级为含风险/稳妥两条路线的 Choice。
- 风险路线选择猎人并投掷一枚 d10：成功获得知识与少量碎石，失败留下手臂普通伤势。
- 稳妥路线无需猎人或判定，稳定获得两份碎石，保证空名册也不会软锁日程。
- 全部结果复用既有表驱动效果、世界空间 3D 事件入口与权威提交边界，不增加 Showdown、GameManager 分支或新存档字段。

## Capabilities

### New Capabilities

- `main-face-echo-choice-arc`: 石脸回声的跨年度桌面选择内容弧。

## Impact

- 正式营地事件表和相关内容、时间线、ActionSession 回归测试。
- 依赖现有事件链、稳定时间线身份、配置化年度边界、实体事件交互和普通伤势闭环。
