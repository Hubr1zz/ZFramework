## Context

`UnlockedByMaterial` 原先读取库存现量，把“是否知道配方”和“是否有材料制造”混成一个状态。素材在 Hunt 携带阶段尚未进入营地权威状态，因此发现事实只能在成功回营或营地事件资源正式提交时产生。

## Decisions

### SettlementInstance 持有素材知识

以稳定物品 ID 保存 `DiscoveredMaterialIds`。规则层通过 `hasDiscoveredMaterial` 判断素材解锁，通过 `getResource` 独立判断制造库存；消费不删除知识。

### 发现只跟随权威资源提交

Hunt collectible 不登记发现。Settlement 回营 Action 和营地事件成功增加资源后登记；初始资源在内容投影时登记。旧 schema 只从正库存补种一次，并按稳定 ID 排序以保持确定性存档。

### 工坊目录在安装事务中失败关闭

目录中的工坊 ID 必须规范且唯一，前置发明与成本物品必须属于当前 Settlement Plan；任何配方引用不存在或未规范的工坊 ID 时拒绝整个 Campaign 内容候选。

## Boundaries

- 多材料配方维持“发现任一材料即可知道配方”的既有语义，后续需要时再引入显式配方知识。
- UI 刷新不进入 ActionQueue；View 继续只投影和提交命令。
- 不推进 Showdown，也不新增通用解锁图或新 MonoBehaviour。
