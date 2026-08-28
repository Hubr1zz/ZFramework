## Context

`HunterState.Insanity` 原本是无界整数，事件效果直接相加；模板只拒绝负数，View 只显示原始数字。设计事实只足以确定默认值、合法范围和三档名称，不足以定义属性修正、事件权重、恢复方式或门禁。

## Goals / Non-Goals

**Goals:**

- 在 GameCore 提供唯一的压抑范围、增长和分类规则。
- 让作者配置错误在内容装配时失败，让历史越界存档可兼容恢复。
- 在现有 3D 猎人信息中提供只读分类，不产生新玩法提交。

**Non-Goals:**

- 不定义疯狂或消极状态的属性效果、AI、事件条件、权重、恢复方法或阈值触发事件。
- 不把所有未显式配置的 JSON 数值改写为 4；0 本身是合法作者值，当前序列化格式无法区分缺字段与显式 0。
- 不新增 Runner、事件类型、UI ActionQueue、MonoBehaviour 生命周期或 Showdown 内容。

## Decisions

- `HunterSuppressionRules` 是纯 C# 权威，定义 0、4、8 和 0–2／3–5／6–8 三档；分类对历史越界输入使用有界投影。
- 正向事件效果必须大于 0，并通过 `Increase` 在加法前比较剩余空间，避免整数溢出并在 8 饱和。
- 作者模板越界直接拒绝；旧档在 `CampaignSaveStorage` 的 canonical snapshot 恢复边界 clamp，不产生 ActionQueue 事实或额外效果。
- 3D View 只读取数值和分类显示名。所有状态变化仍由现有事件事务及其 ActionQueue root 掌权。

## Risks / Trade-offs

- [未来表记录缺少 insanity 字段会被 JsonUtility 解释为 0] → 当前生产猎人均显式配置；未来若要求区分“缺失”和“显式 0”，通过表 schema 升级增加 presence 语义，不在本阶段猜测。
- [替代存档端口绕过归一] → 持久化端口必须返回 canonical snapshot；当前生产文件存档已在恢复边界归一。
- [非法运行态仍可显示原始越界数字] → View 不静默掩盖权威污染，分类保持安全投影；生产入口已由表校验、事件规则和存档恢复封闭。
