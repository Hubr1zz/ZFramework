## Why

资源点生成在高权重类型达到单地块上限后仍会反复抽中无效候选，可能在合法候选尚存时意外欠填；活动狩猎恢复又未核验资源点是否仍符合冻结地块配置。两者会让首次翻图与读档后的资源集合失去同一配置语义。

## What Changes

- 资源点生成每轮只从尚未达到同类上限的有效候选中按权重选择，直到达到地块容量或候选耗尽。
- Hunt Bundle 拒绝同一地块中重复的资源点配置稳定 ID，消除配置归属歧义。
- 活动狩猎恢复核验资源点归属、总量、同类上限、翻牌数与素材池多重集合，失败时不发布部分运行态。
- 保留 schema v2/v3 单素材资源点的受限迁移；schema v4 继续要求完整素材池。
- 不定义设计文档尚未声明的 `0–3` 数量分布，不改变 View、ActionQueue 或生产资产。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `hunt-map-generation`: 补充资源点的受限加权生成契约。
- `hunt-content-bundle-lifecycle`: 补充同一地块资源点稳定 ID 唯一性门禁。
- `active-hunt-persistence`: 补充资源点配置一致性恢复与旧 schema 迁移契约。

## Impact

影响 GameCore 资源生成规则、Hunt Bundle 内容预检、活动狩猎恢复及对应 EditMode 测试。不改变存档结构、地块资产数值、玩家操作入口或阶段生命周期。
