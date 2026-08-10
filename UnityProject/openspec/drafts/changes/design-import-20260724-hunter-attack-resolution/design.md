# 猎人攻击结算实现设计

## 状态与生命周期

HunterAttackResolver 与临时攻击结果牌堆 持有本模块权威状态。纯 C# 层负责确定性规则；现有 GameManager/CombatManager 等 composition root 负责创建和连接，Adapter/ViewLayer 负责 Unity 生命周期、输入与表现。

## 依赖

- 猎人行动卡代码实现：需要接口。
- Boss 部位卡代码实现：需要接口。
- 战场交互代码实现：需要接口。

## 约束

## 测试

以固定输入或随机源覆盖规则、边界和跨模块端口；Unity 侧只验证映射与事件顺序。
