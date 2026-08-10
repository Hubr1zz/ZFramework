# Boss 行动牌堆与目标选择实现设计

## 状态与生命周期

BossActionDeck 与 TargetSelectionPolicy 持有本模块权威状态。纯 C# 层负责确定性规则；现有 composition root 负责创建和连接，Adapter/ViewLayer 负责 Unity 生命周期、输入与表现。

## 依赖

- 战斗回合与时点代码实现：需要接口。
- 战场交互代码实现：需要接口。

## 约束

## 测试

以固定输入或随机源覆盖规则、边界和跨模块端口；Unity 侧只验证映射与事件顺序。
