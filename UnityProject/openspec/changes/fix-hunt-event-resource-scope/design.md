## Context

共享事件系统跨 Settlement 与 Hunt 复用，但资源存在两个互斥权威：营地库存和本次远征携带物。写入端口已经按阶段分离，可用性查询却绕过端口直接读取 `SettlementInstance`，因此玩家看到的选项与最终提交不一致。

## Decisions

### 资源端口同时提供窄只读能力

增加 `IPlayableEventResourceAvailability`，只暴露 `Scope` 与按稳定资源 ID 查询数量。`IPlayableEventResourceCommand` 继承该接口，使同一个阶段对象同时服务于 View 投影、ActionQueue 校验和最终写入，不建立第二份库存服务。

### 作用域严格替换而不是合并

Settlement 读取营地库存；Hunt 只汇总当前存活小队的携带物。Hunt 不得把营地库存作为后备，也不得把两个数量相加。未知资源返回不可用，溢出查询饱和或失败关闭，最终写入仍使用既有预检。

### View 只持有当前提示的只读引用

`IPlayableEventInput.SelectChoiceAsync` 接收只读资源能力。世界空间 View 用它生成禁用原因并在返回选择前重验，结束提示或销毁时清除引用。扣除资源仍只发生在事件 Action 的提交阶段。

### 玩家显式选择失效时失败关闭

玩家选择后，Runner 与事件事务必须用同一阶段端口再次校验。条件已失效时当前节点失败并保留 occurrence，不得静默改选另一条分支。无输入测试宿主的自动选择仍从当前可用选项中选择。

## Risks / Trade-offs

- 修改共享输入接口会影响测试替身；所有实现者必须同步编译验证。
- 小队携带物是多猎人集合，移除仍沿用“优先当前 actor、再其他存活猎人”的既有顺序；本 Change 不新增资源归属选择 UI。
- 本轮不修改事件内容。生产案例只证明现有父奖励能解锁并支付现有子事件安全选项。
